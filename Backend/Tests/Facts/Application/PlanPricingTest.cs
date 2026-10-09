using PhotonBypass.Application.Billing;
using PhotonBypass.Application.Plan;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Static;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockLocalRepository;

namespace PhotonBypass.Test.Facts.Application;

public class PlanPricingTest : UnitLevelServiceInitializer
{
    [Fact]
    public async Task Estimate_TrafficPlan_PricesEffectiveDays()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var calculator = scope.ServiceProvider.GetRequiredService<IPriceCalculator>();

        job_context.InjectJobContext(99, "User99", "User10");

        var estimate = (await plan_app.Estimate("User10", 2, 30, 50)).Data;

        Assert.NotNull(estimate);
        Assert.Equal(100, estimate.Days);
        Assert.Equal(50, estimate.Gigabytes);
        Assert.Equal(await calculator.CalculatePrice(3, 2, 100, 50), estimate.Price);
        Assert.NotEqual(await calculator.CalculatePrice(3, 2, 30, 50), estimate.Price);
    }

    [Theory]
    [InlineData(1, 120)]
    [InlineData(3, 90)]
    [InlineData(5, 80)]
    public async Task Estimate_EffectiveDaysFormula_PerUserCount(byte users, int expected_days)
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var calculator = scope.ServiceProvider.GetRequiredService<IPriceCalculator>();

        job_context.InjectJobContext(99, "User99", "User10");

        var estimate = (await plan_app.Estimate("User10", users, 30, 50)).Data;

        Assert.NotNull(estimate);
        Assert.Equal(expected_days, estimate.Days);
        Assert.Equal(await calculator.CalculatePrice(3, users, expected_days, 50), estimate.Price);
    }

    [Fact]
    public async Task Renewal_IssuesInvoiceWithEffectiveDaysPrice()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var invoice_moq = scope.ServiceProvider.GetRequiredService<InvoiceRepositoryMoq>();

        job_context.InjectJobContext(10, "User10", "User10");

        var estimate = (await plan_app.Estimate("User10", 2, 30, 50)).Data!;
        var result = await plan_app.Renewal("User10", 2, 30, 50);

        Assert.Equal(2, result.Code / 100);
        Assert.NotNull(result.Data);

        var invoice = invoice_moq.Data.Single(i => i.Code == result.Data!.InvoiceCode);
        Assert.Equal(estimate.Price, invoice.TotalPrice);
        Assert.Equal(0, invoice.WalletDeduction);
        Assert.Equal(estimate.Price, invoice.Payable);
    }

    [Fact]
    public async Task SettleWallet_DebitMatchesInvoiceEffectiveDaysPrice()
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
            new PhotonBypass.Domain.Account.Entity.WalletEntity
            {
                Id = 950,
                AccountId = 10,
                Amount = 1000,
                Direction = PhotonBypass.Domain.Account.Model.BalanceDirection.Credit,
                Status = PhotonBypass.Domain.Account.Model.BalanceStatus.Completed,
                Description = "seed",
            },
        ];

        var code = (await plan_app.Renewal("User10", 2, 30, 50)).Data!.InvoiceCode!.Value;

        var invoice = invoice_moq.Data.Single(i => i.Code == code);
        Assert.Equal(260, invoice.TotalPrice);

        var settle_result = await billing_app.SettleWallet(code);

        Assert.Equal(2, settle_result.Code / 100);

        var debit = wallet_moq.Data[10].Single(w =>
            w.Direction == PhotonBypass.Domain.Account.Model.BalanceDirection.Debit);

        Assert.Equal(invoice.TotalPrice, debit.Amount);
        Assert.Equal(code, debit.InvoiceCode);

        var wallet_repo = scope.ServiceProvider.GetRequiredService<PhotonBypass.Domain.Account.IWalletRepository>();

        Assert.Equal(1000 - 260, await wallet_repo.GetBalance(10));
    }
}
