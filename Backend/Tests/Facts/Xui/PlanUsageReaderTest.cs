using FluentAssertions;
using Moq;
using PhotonBypass.Application.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockLocalRepository;

namespace PhotonBypass.Test.Facts.Xui;

public class PlanUsageReaderTest : UnitLevelServiceInitializer
{
    private static readonly ServerEntity XuiServer = new()
    {
        Id = 50,
        IsActive = true,
        RealmId = 3,
        Name = "XuiServer",
        IpAddress = "10.0.0.50",
        DomainName = "xui.photon-bypass.com",
        Features = ServerFeature.Xui | ServerFeature.V2ray,
    };

    private static void UseXuiServer(IServiceScope scope)
    {
        scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>()
            .Setup(repository => repository.GetAllActiveRadius())
            .ReturnsAsync(new List<ServerEntity> { XuiServer });
        scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>()
            .Setup(repository => repository.GetActiveRadiusInRealmOrAll(It.IsAny<int?>()))
            .Returns<int?>(realm_id => Task.FromResult<List<ServerEntity>>(
                realm_id == XuiServer.RealmId ? [XuiServer] : []));
    }

    private static RenewalEntity ReplaceStoredRenewal(RenewalRepositoryMoq renewal_moq, RenewalEntity renewal)
    {
        renewal_moq.Data.RemoveAll(r => r.AccountId == renewal.AccountId);
        renewal_moq.Data.Add(renewal);

        return renewal;
    }

    [Fact]
    public async Task XuiRealmWithStackedRenewal_ReadsCumulativeColumns()
    {
        using var scope = App.Services.CreateScope();
        UseXuiServer(scope);

        var renewal_moq = scope.ServiceProvider.GetRequiredService<RenewalRepositoryMoq>();

        var last_active = DateTime.Now.AddHours(-2);
        var stacked_expiry = DateTime.Now.AddDays(96);
        ReplaceStoredRenewal(renewal_moq, new RenewalEntity
        {
            Id = 1,
            AccountId = 3,
            TrafficLimit = 25 * StaticValues.BytesInGigLong,
            TimeLimitInDays = 30,
            CumulativeTrafficLeft = 45 * StaticValues.BytesInGigLong,
            ExpirationDate = stacked_expiry,
            TrafficUsed = 5 * StaticValues.BytesInGigLong,
            LastActiveTime = last_active,
            LastPollTime = DateTime.Now.AddMinutes(-5),
            LastSeenCounter = 5 * StaticValues.BytesInGigLong,
        });

        var usage = await scope.ServiceProvider.GetRequiredService<IPlanUsageReader>().GetUsage(3);

        usage.Should().NotBeNull();
        usage!.TrafficUsed.Should().Be(5 * StaticValues.BytesInGigLong);
        usage.TrafficLeft.Should().Be(40 * StaticValues.BytesInGigLong);
        usage.LastActiveTime.Should().Be(last_active);
        usage.ExpirationDate.Should().Be(stacked_expiry);
    }

    [Fact]
    public async Task XuiRealmWithPreD16Renewal_FallsBackToTheView()
    {
        using var scope = App.Services.CreateScope();
        UseXuiServer(scope);

        var renewal_moq = scope.ServiceProvider.GetRequiredService<RenewalRepositoryMoq>();
        var plan_moq = scope.ServiceProvider.GetRequiredService<PlanStateRepositoryMoq>();

        // polled renewal written before D16: its own limit is set while the
        // stacked columns are NULL — decision reads fall back to the view
        ReplaceStoredRenewal(renewal_moq, new RenewalEntity
        {
            Id = 1,
            AccountId = 3,
            TrafficLimit = 25 * StaticValues.BytesInGigLong,
            TimeLimitInDays = 30,
            TrafficUsed = 5 * StaticValues.BytesInGigLong,
            LastActiveTime = DateTime.Now.AddHours(-2),
            LastPollTime = DateTime.Now.AddMinutes(-5),
            LastSeenCounter = 5 * StaticValues.BytesInGigLong,
            Created = DateTime.Now.AddDays(-10),
        });

        var plan = new PhotonBypass.Domain.Plan.Entity.PlanStateEntity
        {
            Id = 3,
            Username = "User3",
            RestrictedRealmId = 3,
            TrafficUsed = 111,
            TrafficLeft = 222,
            LastConnectTime = DateTime.Now.AddDays(-1),
            ExpirationDate = DateTime.Now.AddDays(9),
        };

        plan_moq.Setup(repository => repository.GetPlanState(3)).ReturnsAsync(plan);

        var usage = await scope.ServiceProvider.GetRequiredService<IPlanUsageReader>().GetUsage(3);

        usage!.TrafficUsed.Should().Be(5 * StaticValues.BytesInGigLong);
        usage.TrafficLeft.Should().Be(222);
        usage.ExpirationDate.Should().Be(plan.ExpirationDate);
    }

    [Fact]
    public async Task XuiRealmNeverPolled_FallsBackToTheView()
    {
        using var scope = App.Services.CreateScope();
        UseXuiServer(scope);

        var renewal_moq = scope.ServiceProvider.GetRequiredService<RenewalRepositoryMoq>();
        var plan_moq = scope.ServiceProvider.GetRequiredService<PlanStateRepositoryMoq>();

        var renewal = renewal_moq.Data.Single(r => r.AccountId == 3 && r.Id == 1);
        renewal.TrafficUsed = 5 * StaticValues.BytesInGigLong;
        renewal.LastPollTime = null;
        renewal.LastSeenCounter = null;

        var plan = new PhotonBypass.Domain.Plan.Entity.PlanStateEntity
        {
            Id = 3,
            Username = "User3",
            TrafficUsed = 111,
            TrafficLeft = 222,
            LastConnectTime = DateTime.Now.AddDays(-1),
            ExpirationDate = DateTime.Now.AddDays(9),
        };

        plan_moq.Setup(repository => repository.GetPlanState(3)).ReturnsAsync(plan);

        var usage = await scope.ServiceProvider.GetRequiredService<IPlanUsageReader>().GetUsage(3);

        usage!.TrafficUsed.Should().Be(111);
        usage.TrafficLeft.Should().Be(222);
        usage.LastActiveTime.Should().Be(plan.LastConnectTime);
        usage.ExpirationDate.Should().Be(plan.ExpirationDate);
    }

    [Fact]
    public async Task RadiusRealm_KeepsReadingTheView()
    {
        using var scope = App.Services.CreateScope();

        var plan = new PhotonBypass.Domain.Plan.Entity.PlanStateEntity
        {
            Id = 3,
            Username = "User3",
            TrafficUsed = 111,
            TrafficLeft = 222,
            LastConnectTime = DateTime.Now.AddDays(-1),
            ExpirationDate = DateTime.Now.AddDays(9),
        };

        scope.ServiceProvider.GetRequiredService<PlanStateRepositoryMoq>()
            .Setup(repository => repository.GetPlanState(3)).ReturnsAsync(plan);

        var usage = await scope.ServiceProvider.GetRequiredService<IPlanUsageReader>().GetUsage(3);

        usage!.TrafficUsed.Should().Be(111);
        usage.TrafficLeft.Should().Be(222);
    }

    [Fact]
    public async Task XuiRealmUnlimitedTraffic_TrafficLeftIsNull()
    {
        using var scope = App.Services.CreateScope();
        UseXuiServer(scope);

        var renewal_moq = scope.ServiceProvider.GetRequiredService<RenewalRepositoryMoq>();

        var renewal = renewal_moq.Data.Single(r => r.AccountId == 3 && r.Id == 1);
        renewal.TrafficLimit = null;
        renewal.TimeLimitInDays = 30;
        renewal.TrafficUsed = 5 * StaticValues.BytesInGigLong;
        renewal.LastPollTime = DateTime.Now.AddMinutes(-5);

        var usage = await scope.ServiceProvider.GetRequiredService<IPlanUsageReader>().GetUsage(3);

        usage!.TrafficLeft.Should().BeNull();
    }

    [Fact]
    public async Task BatchRead_MixesXuiAndRadiusAccounts()
    {
        using var scope = App.Services.CreateScope();
        UseXuiServer(scope);

        var renewal_moq = scope.ServiceProvider.GetRequiredService<RenewalRepositoryMoq>();

        var renewal = renewal_moq.Data.Single(r => r.AccountId == 3 && r.Id == 1);
        renewal.TrafficLimit = 25 * StaticValues.BytesInGigLong;
        renewal.TrafficUsed = 5 * StaticValues.BytesInGigLong;
        renewal.LastPollTime = DateTime.Now.AddMinutes(-5);
        renewal.LastSeenCounter = 5 * StaticValues.BytesInGigLong;

        var usage_dictionary = await scope.ServiceProvider.GetRequiredService<IPlanUsageReader>()
            .GetUsage([3, 99]);

        usage_dictionary.Should().ContainKey(3);
        usage_dictionary.Should().ContainKey(99);

        usage_dictionary[3].TrafficUsed.Should().Be(5 * StaticValues.BytesInGigLong);
        usage_dictionary[99].TrafficLeft.Should().Be(1073001004);
    }
}
