using FluentAssertions;
using Microsoft.Extensions.Hosting;
using Moq;
using PhotonBypass.Application.Plan;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockLocalRepository;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Facts.Application;

/// <summary>
/// D16 (docs/17) — renewal stacking: expiry = max(now, current expiry) + days,
/// traffic quota = clamped cumulative remaining + the new share. Both values
/// are frozen once on the renewal row and handed to the radius push unchanged.
/// </summary>
public class RenewalStackingTest : UnitLevelServiceInitializer
{
    private const long Gig = StaticValues.BytesInGigLong;

    private static RenewalEntity? pushed_renewal;

    private readonly Mock<IAccountRadiusSyncService> radius_moq = CreateRadiusMoq();

    public RenewalStackingTest()
    {
        pushed_renewal = null;
    }

    protected override void AddClassTestServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddLazyTransient(provider => radius_moq.Object);
    }

    [Fact]
    public async Task EarlyRenewalWithRemaining_StacksTrafficAndExpiry()
    {
        var current_expiry = DateTime.Now.AddDays(10);

        var renewal = await BuyPlan(CurrentPlan(
            trafficLeft: 20 * Gig, trafficLimit: 25 * Gig, expiration: current_expiry));

        renewal.CumulativeTrafficLeft.Should().Be(45 * Gig);
        renewal.ExpirationDate.Should().Be(current_expiry.AddDays(renewal.TimeLimitInDays!.Value));

        // the radius push carries the stacked renewal itself (D16 push path)
        pushed_renewal.Should().BeSameAs(renewal);
        pushed_renewal!.CumulativeTrafficLeft.Should().Be(45 * Gig);
    }

    [Fact]
    public async Task ExpiredPlan_RestartsExpiryFromNowButKeepsTrafficRemaining()
    {
        DateTime before = DateTime.Now;

        var renewal = await BuyPlan(CurrentPlan(
            trafficLeft: 3 * Gig, trafficLimit: 25 * Gig, expiration: DateTime.Now.AddDays(-5)));

        renewal.CumulativeTrafficLeft.Should().Be(28 * Gig);
        renewal.ExpirationDate.Should().BeOnOrAfter(before.AddDays(renewal.TimeLimitInDays!.Value))
            .And.BeOnOrBefore(DateTime.Now.AddDays(renewal.TimeLimitInDays!.Value));
    }

    [Fact]
    public async Task UnlimitedCurrentPlan_MakesStackedQuotaUnlimitedButStacksExpiry()
    {
        var current_expiry = DateTime.Now.AddDays(3);

        var renewal = await BuyPlan(CurrentPlan(
            trafficLeft: null, trafficLimit: null, timeLimitInDays: 30, expiration: current_expiry));

        renewal.CumulativeTrafficLeft.Should().BeNull();
        renewal.ExpirationDate.Should().Be(current_expiry.AddDays(renewal.TimeLimitInDays!.Value));
    }

    [Fact]
    public async Task RealmChangeAtRenewal_KeepsTheStackedRemaining()
    {
        var renewal = await BuyPlan(CurrentPlan(
            trafficLeft: 20 * Gig, trafficLimit: 25 * Gig,
            expiration: DateTime.Now.AddDays(10), realm_id: 2, last_connect: DateTime.Now.AddDays(-10)));

        // stale activity (older than 7 days) moves the plan to a fresh realm —
        // the stacked remaining travels with it
        renewal.RestrictedRealmId.Should().NotBe(2);
        renewal.CumulativeTrafficLeft.Should().Be(45 * Gig);
    }

    [Fact]
    public async Task FirstPurchase_StacksNothingButTheOwnShare()
    {
        DateTime before = DateTime.Now;

        var renewal = await BuyPlan(new PlanStateEntity
        {
            Id = 2,
            Username = "User2",
            IsActive = true,
        });

        renewal.CumulativeTrafficLeft.Should().Be(25 * Gig);
        renewal.ExpirationDate.Should().BeOnOrAfter(before.AddDays(renewal.TimeLimitInDays!.Value))
            .And.BeOnOrBefore(DateTime.Now.AddDays(renewal.TimeLimitInDays!.Value));
    }

    [Fact]
    public async Task OverdrawnTraffic_ClampsAtZeroBeforeStacking()
    {
        var renewal = await BuyPlan(CurrentPlan(
            trafficLeft: -5 * Gig, trafficLimit: 25 * Gig, expiration: DateTime.Now.AddDays(10)));

        renewal.CumulativeTrafficLeft.Should().Be(25 * Gig);
    }

    private static PlanStateEntity CurrentPlan(
        long? trafficLeft, long? trafficLimit, DateTime? expiration,
        short? timeLimitInDays = 120, int? realm_id = null, DateTime? last_connect = null)
    {
        return new PlanStateEntity
        {
            Id = 2,
            Username = "User2",
            IsActive = true,
            SimultaneousUser = 1,
            RestrictedRealmId = realm_id,
            TrafficLimit = trafficLimit,
            TimeLimitInDays = timeLimitInDays,
            LastConnectTime = last_connect,
            ExpirationDate = expiration,
            TrafficLeft = trafficLeft,
        };
    }

    private async Task<RenewalEntity> BuyPlan(PlanStateEntity current_plan)
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var wallet_moq = scope.ServiceProvider.GetRequiredService<WalletRepositoryMoq>();

        // replay path: payer 99 buys a 25G/2u plan for target User2
        job_context.InjectJobContext(99, "User99", "User2");

        wallet_moq.Data[99] =
        [
            new WalletEntity
            {
                Id = 903,
                AccountId = 99,
                Amount = 1500,
                Direction = BalanceDirection.Credit,
                Status = BalanceStatus.Completed,
                Description = "seed",
            },
        ];

        scope.ServiceProvider.GetRequiredService<PlanStateRepositoryMoq>()
            .Setup(repository => repository.GetPlanState(2))
            .ReturnsAsync(current_plan);

        var result = await plan_app.Renewal(99, 1, "User2t|2u|120d|25g");

        result.Code.Should().BeGreaterThanOrEqualTo(200);
        result.Code.Should().BeLessThan(300);

        return pushed_renewal ?? throw new InvalidOperationException("the radius push never ran");
    }

    private static Mock<IAccountRadiusSyncService> CreateRadiusMoq()
    {
        var moq = new Mock<IAccountRadiusSyncService>();

        moq.Setup(x => x.SyncUserAndActive(It.IsAny<AccountEntity>(), It.IsAny<RenewalEntity>()))
            .Callback<AccountEntity, RenewalEntity>((_, renewal) => pushed_renewal = renewal)
            .Returns(Task.CompletedTask);

        return moq;
    }
}
