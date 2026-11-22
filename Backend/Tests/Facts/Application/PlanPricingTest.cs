using FluentAssertions;
using Moq;
using PhotonBypass.Application.Billing;
using PhotonBypass.Application.Plan;
using PhotonBypass.Application.Plan.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Static;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockLocalRepository;

namespace PhotonBypass.Test.Facts.Application;

/// <summary>
/// Docs/24 §2: the calculator decides the plan-undefined axes. The traffic-
/// only fixture plans (1/2) derive the effective days and the simultaneous-
/// user policy from the traffic share — the composed estimate carries them,
/// and the invoice freezes the calculator output (D9).
/// </summary>
public class PlanPricingTest : UnitLevelServiceInitializer
{
    private static PurchaseRequest TrafikiEconomy(int gigabytes, int? realm = null)
    {
        return new PurchaseRequest
        {
            PlanId = 2,
            Gigabytes = gigabytes,
            RealmId = realm,
        };
    }

    [Fact]
    public async Task Estimate_TrafficPlan_CalculatorDecidesDaysAndUsers()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        job_context.InjectJobContext(99, "User99", "User10");

        var estimate = (await plan_app.Estimate(10, TrafikiEconomy(50))).Data;

        Assert.NotNull(estimate);

        // plan 2: 40 + (50/25) * 40; days = 60 + packages * 30; users = 2
        Assert.Equal(120, estimate.Price);
        Assert.Equal(120, estimate.Days);
        Assert.Equal(50, estimate.Gigabytes);
        Assert.Equal(2, estimate.SimultaneousUserCount);
    }

    [Fact]
    public async Task Estimate_TrafficPlan_CalculatorMayLeaveAnAxisUnlimited()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();

        job_context.InjectJobContext(99, "User99", "User10");

        // plan 1 prices the same share differently and leaves users unlimited
        var estimate = (await plan_app.Estimate(10, new PurchaseRequest
        {
            PlanId = 1,
            Gigabytes = 50,
        })).Data;

        Assert.NotNull(estimate);
        Assert.Equal(160, estimate.Price); // 60 + (50/25) * 50
        Assert.Equal(120, estimate.Days);
        Assert.Null(estimate.SimultaneousUserCount);
    }

    [Fact]
    public async Task Estimate_WarningWhenTheUnlimitedCurrentAxisShadowsThePurchase()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var plan_state_moq = scope.ServiceProvider.GetRequiredService<PlanStateRepositoryMoq>();

        job_context.InjectJobContext(99, "User99", "User10");

        // the running band is unlimited on both axes (D5: null dominates)
        plan_state_moq.Setup(repository => repository.GetPlanState(10))
            .ReturnsAsync(new Domain.Plan.Entity.PlanStateEntity
            {
                Id = 10,
                Username = "User10",
                Status = VpnAccountStatus.Active,
                SimultaneousUser = 1,
            });

        var estimate = (await plan_app.Estimate(10, TrafikiEconomy(50))).Data;

        Assert.NotNull(estimate);
        estimate.Warnings.Should().HaveCount(2);
    }

    [Fact]
    public async Task Renewal_IssuesInvoiceWithTheCalculatorPrice()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var invoice_moq = scope.ServiceProvider.GetRequiredService<InvoiceRepositoryMoq>();

        job_context.InjectJobContext(10, "User10", "User10");

        var estimate = (await plan_app.Estimate(10, TrafikiEconomy(50))).Data!;

        // the account has no recorded activity, so it may be stale (Docs/22
        // D6): the renewal carries a realm choice; realm 1 is the fixture's
        // active radius realm
        var result = await plan_app.Renewal(10, TrafikiEconomy(50, realm: 1));

        Assert.Equal(2, result.Code / 100);
        Assert.NotNull(result.Data);

        var invoice = invoice_moq.Data.Single(i => i.Code == result.Data!.InvoiceCode);
        Assert.Equal(estimate.Price, invoice.TotalPrice);
        Assert.Equal(0, invoice.WalletDeduction);
        Assert.Equal(estimate.Price, invoice.Payable);

        // D9: the action freezes the calculator-determined days and users too
        invoice.Action.Should().Be("User10t|2p|2u|120d|50g|1r");
    }

    [Fact]
    public async Task SettleWallet_DebitMatchesTheFrozenInvoicePrice()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var billing_app = scope.ServiceProvider.GetRequiredService<IBillingApplication>();
        var wallet_moq = scope.ServiceProvider.GetRequiredService<WalletRepositoryMoq>();
        var invoice_moq = scope.ServiceProvider.GetRequiredService<InvoiceRepositoryMoq>();

        job_context.InjectJobContext(10, "User10", "User10");

        wallet_moq.Data[10] =
        [
            new WalletEntity
            {
                Id = 950,
                AccountId = 10,
                Amount = 1000,
                Direction = BalanceDirection.Credit,
                Status = BalanceStatus.Completed,
                Description = "seed",
            },
        ];

        var code = (await plan_app.Renewal(10, TrafikiEconomy(50, realm: 1))).Data!.InvoiceCode!.Value;

        var invoice = invoice_moq.Data.Single(i => i.Code == code);
        Assert.Equal(120, invoice.TotalPrice);

        var settle_result = await billing_app.SettleWallet(code);

        Assert.Equal(2, settle_result.Code / 100);

        var debit = wallet_moq.Data[10].Single(w => w.Direction == BalanceDirection.Debit);

        Assert.Equal(invoice.TotalPrice, debit.Amount);
        Assert.Equal(code, debit.InvoiceCode);

        var wallet_repo = scope.ServiceProvider.GetRequiredService<Domain.Account.IWalletRepository>();

        Assert.Equal(1000 - 120, await wallet_repo.GetBalance(10));
    }
}
