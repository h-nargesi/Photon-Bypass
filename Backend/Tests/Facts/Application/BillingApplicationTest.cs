using PhotonBypass.Application.Billing;
using PhotonBypass.Application.Billing.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account.Model;
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

        job_context.InjectJobContext(1, "User1", "User1");

        var invoice_code = await billing_repo.GenerateInvoiceCode(new NewInvoiceInfo
        {
            Action = "User1t|2u|120d|50g",
            Price = 300,
            Descripttion = "",
        });
        var invoice = (await billing_repo.GetInvoice(invoice_code.Data)).Data;

        Assert.NotNull(invoice);
        Assert.Equal("I100004", invoice.Code);
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
        Assert.Equal("I100004", invoice.Code);
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
        Assert.Equal("I100004", invoice.Code);
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
        Assert.Equal("I100004", invoice.Code);
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
        Assert.Equal("I100004", invoice.Code);
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
        Assert.Equal("I100004", invoice.Code);
        Assert.Equal(1900, invoice.TotalSum);
        Assert.Equal(BalanceStatus.Pending, invoice.Status);
        Assert.Equal(3, invoice.InvoiceItems.Length);
        Assert.Equal(800, invoice.InvoiceItems[0].Value);
        Assert.Equal(800, invoice.InvoiceItems[1].Value);
        Assert.Equal(300, invoice.InvoiceItems[2].Value);
    }

    [Fact(Skip = "NotImplementedException")]
    public async Task PaymentCallback()
    {
        using var scope = App.Services.CreateScope();
        var billing_repo = scope.ServiceProvider.GetRequiredService<IBillingApplication>();

        throw new NotImplementedException();
    }
}
