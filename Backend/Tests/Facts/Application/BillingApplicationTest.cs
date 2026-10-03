using PhotonBypass.Application.Billing;
using PhotonBypass.Application.Billing.Model;
using PhotonBypass.Application.Plan;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Plan;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.Application;

public class BillingApplicationTest : UnitLevelServiceInitializer
{
    [Fact]
    public async Task GenerateInvoiceCode_not_paid_renewal()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_repo = scope.ServiceProvider.GetRequiredService<IBillingApplication>();

        job_context.InjectJobContext(3, "User3", "User3");

        var invoice_code = (await billing_repo.GenerateInvoiceCode()).Data;
        var invoice = (await billing_repo.GetInvoice(invoice_code)).Data;

        Assert.NotNull(invoice);
        Assert.Equal(800, invoice.TotalSum);
        Assert.Equal(BalanceStatus.Pending, invoice.Status);
        Assert.Single(invoice.InvoiceItems);
        Assert.Equal(800, invoice.InvoiceItems[0].Value);
    }

    [Fact]
    public async Task GenerateInvoiceCode_new_info()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_repo = scope.ServiceProvider.GetRequiredService<IBillingApplication>();

        job_context.InjectJobContext(99, "User99", "User99");

        var invoice_code = await billing_repo.GenerateInvoiceCode(new NewInvoiceInfo
        {
            Action = "User1t|2u|120d|50g",
            Price = 300,
            Descripttion = "",
        });
        var invoice = (await billing_repo.GetInvoice(invoice_code.Data)).Data;

        Assert.NotNull(invoice);
        Assert.Equal(100004, invoice.Code);
        Assert.Equal(300, invoice.TotalSum);
        Assert.Equal(BalanceStatus.Pending, invoice.Status);
        Assert.Single(invoice.InvoiceItems);
        Assert.Equal(300, invoice.InvoiceItems[0].Value);
    }

    [Fact]
    public async Task GenerateInvoiceCode_not_paid_wallet()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_repo = scope.ServiceProvider.GetRequiredService<IBillingApplication>();

        job_context.InjectJobContext(2, "User2", "User2");

        var invoice_code = (await billing_repo.GenerateInvoiceCode()).Data;
        
        Assert.Null(invoice_code);
    }

    [Fact]
    public async Task GenerateInvoiceCode_not_paid_wallet_renewal_max_invoice_code()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_repo = scope.ServiceProvider.GetRequiredService<IBillingApplication>();

        job_context.InjectJobContext(4, "User4", "User4");

        var invoice_code = (await billing_repo.GenerateInvoiceCode()).Data;
        var invoice = (await billing_repo.GetInvoice(invoice_code)).Data;

        Assert.NotNull(invoice);
        Assert.Equal(100004, invoice.Code);
        Assert.Equal(800, invoice.TotalSum);
        Assert.Equal(BalanceStatus.Pending, invoice.Status);
        Assert.Single(invoice.InvoiceItems);
        Assert.Equal(800, invoice.InvoiceItems[0].Value);
    }

    [Fact]
    public async Task GenerateInvoiceCode_not_paid_wallet_renewal_without_invoice_code()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_repo = scope.ServiceProvider.GetRequiredService<IBillingApplication>();

        job_context.InjectJobContext(5, "User5", "User5");

        var invoice_code = (await billing_repo.GenerateInvoiceCode()).Data;
        var invoice = (await billing_repo.GetInvoice(invoice_code)).Data;

        Assert.NotNull(invoice);
        Assert.Equal(100004, invoice.Code);
        Assert.Equal(800, invoice.TotalSum);
        Assert.Equal(BalanceStatus.Pending, invoice.Status);
        Assert.Single(invoice.InvoiceItems);
        Assert.Equal(800, invoice.InvoiceItems[0].Value);
    }

    [Fact]
    public async Task GenerateInvoiceCode_all_match_less_wallet_price()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_repo = scope.ServiceProvider.GetRequiredService<IBillingApplication>();

        job_context.InjectJobContext(8, "User8", "User8");

        var invoice_code = await billing_repo.GenerateInvoiceCode(new NewInvoiceInfo
        {
            Action = "User1t|2u|120d|50g",
            Price = 300,
            Descripttion = "",
        });
        var invoice = (await billing_repo.GetInvoice(invoice_code.Data)).Data;

        Assert.NotNull(invoice);
        Assert.Equal(100004, invoice.Code);
        Assert.Equal(700, invoice.TotalSum);
        Assert.Equal(BalanceStatus.Pending, invoice.Status);
        Assert.Equal(2, invoice.InvoiceItems.Length);
        Assert.Equal(400, invoice.InvoiceItems[0].Value);
        Assert.Equal(300, invoice.InvoiceItems[1].Value);
    }

    [Fact]
    public async Task GenerateInvoiceCode_all_match_greater_wallet_price()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_repo = scope.ServiceProvider.GetRequiredService<IBillingApplication>();

        job_context.InjectJobContext(9, "User9", "User9");

        var invoice_code = await billing_repo.GenerateInvoiceCode(new NewInvoiceInfo
        {
            Action = "User1t|2u|120d|50g",
            Price = 300,
            Descripttion = "",
        });
        var invoice = (await billing_repo.GetInvoice(invoice_code.Data)).Data;

        Assert.NotNull(invoice);
        Assert.Equal(100004, invoice.Code);
        Assert.Equal(2300, invoice.TotalSum);
        Assert.Equal(BalanceStatus.Pending, invoice.Status);
        Assert.Equal(2, invoice.InvoiceItems.Length);
        Assert.Equal(2000, invoice.InvoiceItems[0].Value);
        Assert.Equal(300, invoice.InvoiceItems[1].Value);
    }

    [Fact]
    public async Task GenerateInvoiceCode_all_match_same_price()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_repo = scope.ServiceProvider.GetRequiredService<IBillingApplication>();

        job_context.InjectJobContext(6, "User6", "User6");

        var invoice_code = await billing_repo.GenerateInvoiceCode(new NewInvoiceInfo
        {
            Action = "User1t|2u|120d|50g",
            Price = 300,
            Descripttion = "",
        });
        var invoice = (await billing_repo.GetInvoice(invoice_code.Data)).Data;

        Assert.NotNull(invoice);
        Assert.Equal(100004, invoice.Code);
        Assert.Equal(1900, invoice.TotalSum);
        Assert.Equal(BalanceStatus.Pending, invoice.Status);
        Assert.Equal(3, invoice.InvoiceItems.Length);
        Assert.Equal(800, invoice.InvoiceItems[0].Value);
        Assert.Equal(800, invoice.InvoiceItems[1].Value);
        Assert.Equal(300, invoice.InvoiceItems[2].Value);
    }

    [Fact]
    public async Task PaymentCallback()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_repo = scope.ServiceProvider.GetRequiredService<IBillingApplication>();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
        var renewal_repo = scope.ServiceProvider.GetRequiredService<IRenewalRepository>();

        job_context.InjectJobContext(99, "User99", "User99");

        var estimate = (await plan_app.Estimate("User99", 2, 120, 50)).Data?.Price ?? 0;

        var invoice_code = await billing_repo.GenerateInvoiceCode(new NewInvoiceInfo
        {
            Action = "User99t|2u|120d|50g",
            Price = 300,
            Descripttion = "",
        });

        Assert.NotNull(invoice_code.Data);
        Assert.Equal(2, invoice_code.Code / 100);

        var result = await billing_repo.PaymentCallback(invoice_code.Data.Value.ToString());

        Assert.Equal(2, result.Code / 100);
        Assert.Equal(BalanceStatus.Completed, result.Data);

        var invoice_items = await wallet_repo.GetInvoice(invoice_code.Data.Value);
        Assert.All(invoice_items, i => Assert.Equal(BalanceStatus.Completed, i.Status));

        var credit = invoice_items.Single(i => !string.IsNullOrEmpty(i.Action));

        Assert.NotNull(await renewal_repo.GetByWalletCredit(credit.Id));

        Assert.Equal(300 - estimate, await wallet_repo.GetBalance(99));
    }

    [Fact]
    public async Task PaymentCallback_Twice_RenewalOnlyOnce()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_repo = scope.ServiceProvider.GetRequiredService<IBillingApplication>();
        var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
        var renewal_repo = scope.ServiceProvider.GetRequiredService<IRenewalRepository>();

        job_context.InjectJobContext(99, "User99", "User99");

        var invoice_code = await billing_repo.GenerateInvoiceCode(new NewInvoiceInfo
        {
            Action = "User99t|2u|120d|50g",
            Price = 300,
            Descripttion = "",
        });

        Assert.NotNull(invoice_code.Data);

        var first = await billing_repo.PaymentCallback(invoice_code.Data.Value.ToString());
        Assert.Equal(2, first.Code / 100);

        var balance_after_first = await wallet_repo.GetBalance(99);
        var transactions_after_first = await wallet_repo.GetTransactions(99);
        var debits_after_first = transactions_after_first
            .Where(w => w.Direction == BalanceDirection.Debit && w.Status == BalanceStatus.Completed)
            .ToList();

        Assert.Single(debits_after_first);

        var second = await billing_repo.PaymentCallback(invoice_code.Data.Value.ToString());

        Assert.Equal(2, second.Code / 100);
        Assert.Equal(BalanceStatus.Completed, second.Data);

        Assert.Equal(balance_after_first, await wallet_repo.GetBalance(99));

        var transactions_after_second = await wallet_repo.GetTransactions(99);
        var debits_after_second = transactions_after_second
            .Where(w => w.Direction == BalanceDirection.Debit && w.Status == BalanceStatus.Completed)
            .ToList();

        Assert.Single(debits_after_second);
        Assert.Equal(debits_after_first[0].Id, debits_after_second[0].Id);
    }

    [Fact]
    public async Task PaymentCallback_InvalidToken()
    {
        using var scope = App.Services.CreateScope();
        var billing_repo = scope.ServiceProvider.GetRequiredService<IBillingApplication>();

        var result = await billing_repo.PaymentCallback("not-a-number");

        Assert.Equal(400, result.Code);
        Assert.Equal(BalanceStatus.Failed, result.Data);
    }

    [Fact]
    public async Task GenerateInvoiceCode_ValueBounds()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_repo = scope.ServiceProvider.GetRequiredService<IBillingApplication>();

        job_context.InjectJobContext(99, "User99", "User99");

        await Assert.ThrowsAsync<UserException>(() => billing_repo.GenerateInvoiceCode(0));
        await Assert.ThrowsAsync<UserException>(() => billing_repo.GenerateInvoiceCode(-5));
        await Assert.ThrowsAsync<UserException>(() => billing_repo.GenerateInvoiceCode(100001));

        var result = await billing_repo.GenerateInvoiceCode(500);

        Assert.Equal(2, result.Code / 100);
        Assert.Equal(100004, result.Data);
    }

    [Fact]
    public async Task Renewal_InvalidInput_NoPendingWallet()
    {
        using var scope = App.Services.CreateScope();
        var plan_app = scope.ServiceProvider.GetRequiredService<IPlanApplication>();
        var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();

        await Assert.ThrowsAsync<UserException>(() => plan_app.Renewal("User3", 2, 120, 1));

        Assert.Empty(await wallet_repo.GetNotPaid(3));
    }
}
