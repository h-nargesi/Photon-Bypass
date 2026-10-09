using PhotonBypass.Application.Billing;
using PhotonBypass.Application.Management;
using PhotonBypass.Application.Management.Model;
using PhotonBypass.Application.Plan;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Plan;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockLocalRepository;

namespace PhotonBypass.Test.Facts.Application;

public class BillingApplicationTest : UnitLevelServiceInitializer
{
    [Fact]
    public async Task IssueTopUp_ValueBounds()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_app = scope.ServiceProvider.GetRequiredService<IBillingApplication>();

        job_context.InjectJobContext(99, "User99", "User99");

        await Assert.ThrowsAsync<UserException>(() => billing_app.IssueTopUp(0));
        await Assert.ThrowsAsync<UserException>(() => billing_app.IssueTopUp(-5));
        await Assert.ThrowsAsync<UserException>(() => billing_app.IssueTopUp(100001));

        var result = await billing_app.IssueTopUp(500);

        Assert.Equal(2, result.Code / 100);
        Assert.Equal(InvoiceRepositoryMoq.StartCode + 1, result.Data);
    }

    [Fact]
    public async Task IssueTopUp_CancelsPreviousPending()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_app = scope.ServiceProvider.GetRequiredService<IBillingApplication>();
        var invoice_moq = scope.ServiceProvider.GetRequiredService<InvoiceRepositoryMoq>();

        job_context.InjectJobContext(99, "User99", "User99");

        var first = await billing_app.IssueTopUp(500);
        var second = await billing_app.IssueTopUp(800);

        Assert.NotEqual(first.Data, second.Data);

        var first_invoice = invoice_moq.Data.Single(i => i.Code == first.Data);
        var second_invoice = invoice_moq.Data.Single(i => i.Code == second.Data);

        Assert.Equal(BalanceStatus.Canceled, first_invoice.Status);
        Assert.Equal(BalanceStatus.Pending, second_invoice.Status);
        Assert.Equal(InvoiceKind.TopUp, second_invoice.Kind);
        Assert.Equal(800, second_invoice.TotalPrice);
        Assert.Equal(800, second_invoice.Payable);
    }

    [Fact]
    public async Task IssuePlanInvoice_Netting()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_app = scope.ServiceProvider.GetRequiredService<IBillingApplication>();
        var invoice_moq = scope.ServiceProvider.GetRequiredService<InvoiceRepositoryMoq>();

        job_context.InjectJobContext(3, "User3", "User3");

        var negative_balance = await billing_app.IssuePlanInvoice(500, "User3t|2u|120d|50g", "پلن تست");

        var negative_invoice = invoice_moq.Data.Single(i => i.Code == negative_balance.Data);
        Assert.Equal(InvoiceKind.Plan, negative_invoice.Kind);
        Assert.Equal(0, negative_invoice.WalletDeduction);
        Assert.Equal(1300, negative_invoice.Payable);

        job_context.InjectJobContext(2, "User2", "User2");

        var rich = await billing_app.IssuePlanInvoice(800, "User2t|2u|120d|50g", "پلن تست");

        var rich_invoice = invoice_moq.Data.Single(i => i.Code == rich.Data);
        Assert.Equal(800, rich_invoice.WalletDeduction);
        Assert.Equal(-700, rich_invoice.Payable);
    }

    [Fact]
    public async Task GetInvoice_OwnershipByTarget()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_app = scope.ServiceProvider.GetRequiredService<IBillingApplication>();
        var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();

        job_context.InjectJobContext(2, "User2", "User2");

        var code = (await billing_app.IssuePlanInvoice(800, "User2t|2u|120d|50g", "پلن تست")).Data;

        var invoice = (await billing_app.GetInvoice(code!.Value)).Data;

        Assert.NotNull(invoice);
        Assert.Equal(code, invoice.Code);
        Assert.Equal(InvoiceKind.Plan, invoice.Kind);
        Assert.Equal(800, invoice.TotalPrice);
        Assert.Equal(800, invoice.WalletDeduction);
        Assert.Equal(-700, invoice.Payable);
        Assert.Equal(await wallet_repo.GetBalance(2), invoice.WalletBalance);
        Assert.True(invoice.AllowWallet);
        Assert.False(invoice.HasReceipt);
        Assert.Single(invoice.CardInfo);
        Assert.Equal(2, invoice.Items.Length);
        Assert.Equal(800, invoice.Items[0].Value);
        Assert.Equal(-800, invoice.Items[1].Value);

        job_context.InjectJobContext(3, "User3", "User3");

        await Assert.ThrowsAsync<UserException>(() => billing_app.GetInvoice(code.Value));
    }

    [Fact]
    public async Task RegisterReceipt_Xor()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_app = scope.ServiceProvider.GetRequiredService<IBillingApplication>();

        job_context.InjectJobContext(99, "User99", "User99");

        var code = (await billing_app.IssueTopUp(500)).Data!.Value;
        var image = new byte[] { 1, 2, 3 };

        await Assert.ThrowsAsync<UserException>(() =>
            billing_app.RegisterReceipt(code, image, "receipt.jpg", "image/jpeg", "some text"));

        await Assert.ThrowsAsync<UserException>(() =>
            billing_app.RegisterReceipt(code, null, null, null, null));
    }

    [Fact]
    public async Task RegisterReceipt_ImageValidation()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_app = scope.ServiceProvider.GetRequiredService<IBillingApplication>();

        job_context.InjectJobContext(99, "User99", "User99");

        var code = (await billing_app.IssueTopUp(500)).Data!.Value;
        var image = new byte[] { 1, 2, 3 };

        await Assert.ThrowsAsync<UserException>(() =>
            billing_app.RegisterReceipt(code, image, "receipt.gif", "image/gif", null));

        await Assert.ThrowsAsync<UserException>(() =>
            billing_app.RegisterReceipt(code, image, "receipt.jpg", "text/html", null));

        var long_text = new string('x', 1001);

        await Assert.ThrowsAsync<UserException>(() =>
            billing_app.RegisterReceipt(code, null, null, null, long_text));

        var big_image = new byte[2_097_153];

        await Assert.ThrowsAsync<UserException>(() =>
            billing_app.RegisterReceipt(code, big_image, "receipt.jpg", "image/jpeg", null));
    }

    [Fact]
    public async Task RegisterReceipt_TopUp_CreditVerifying()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_app = scope.ServiceProvider.GetRequiredService<IBillingApplication>();
        var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
        var invoice_moq = scope.ServiceProvider.GetRequiredService<InvoiceRepositoryMoq>();

        job_context.InjectJobContext(99, "User99", "User99");

        var balance_before = await wallet_repo.GetBalance(99);

        var code = (await billing_app.IssueTopUp(500)).Data!.Value;

        var result = await billing_app.RegisterReceipt(code, null, null, null, "receipt text");

        Assert.Equal(2, result.Code / 100);

        var invoice = invoice_moq.Data.Single(i => i.Code == code);
        Assert.Equal(BalanceStatus.Verifying, invoice.Status);
        Assert.Equal("receipt text", invoice.ReceiptText);
        Assert.NotNull(invoice.ReceiptAt);
        Assert.True(invoice.HasReceipt);

        var credit = (await wallet_repo.GetTransactions(99)).Single(w => w.InvoiceCode == code);
        Assert.Equal(BalanceDirection.Credit, credit.Direction);
        Assert.Equal(BalanceStatus.Verifying, credit.Status);
        Assert.Equal(500, credit.Amount);

        Assert.Equal(balance_before + 500, await wallet_repo.GetBalance(99));

        await Assert.ThrowsAsync<UserException>(() =>
            billing_app.RegisterReceipt(code, null, null, null, "second receipt"));
    }

    [Fact]
    public async Task RegisterReceipt_Plan_InsufficientBalance_RenewalOnce()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_app = scope.ServiceProvider.GetRequiredService<IBillingApplication>();
        var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
        var renewal_repo = scope.ServiceProvider.GetRequiredService<IRenewalRepository>();
        var invoice_moq = scope.ServiceProvider.GetRequiredService<InvoiceRepositoryMoq>();

        job_context.InjectJobContext(99, "User99", "User99");

        var code = (await billing_app.IssuePlanInvoice(180, "User99t|2u|120d|50g", "پلن تست")).Data!.Value;

        var result = await billing_app.RegisterReceipt(code, null, null, null, "receipt text");

        Assert.Equal(2, result.Code / 100);

        var credit = (await wallet_repo.GetTransactions(99)).Single(w => w.InvoiceCode == code && w.Direction == BalanceDirection.Credit);

        Assert.NotNull(await renewal_repo.GetByWalletCredit(credit.Id));

        var transactions = await wallet_repo.GetTransactions(99);
        var debits = transactions.Where(w => w.Direction == BalanceDirection.Debit).ToList();

        Assert.Single(debits);
        Assert.Equal(BalanceStatus.Completed, debits[0].Status);
        Assert.Equal(180, debits[0].Amount);
        Assert.Equal(code, debits[0].InvoiceCode);

        Assert.Equal(0, await wallet_repo.GetBalance(99));

        Assert.Equal(BalanceStatus.Verifying, invoice_moq.Data.Single(i => i.Code == code).Status);
    }

    [Fact]
    public async Task SettleWallet_Success()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_app = scope.ServiceProvider.GetRequiredService<IBillingApplication>();
        var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
        var invoice_moq = scope.ServiceProvider.GetRequiredService<InvoiceRepositoryMoq>();

        job_context.InjectJobContext(2, "User2", "User2");

        var code = (await billing_app.IssuePlanInvoice(180, "User2t|2u|120d|50g", "پلن تست")).Data!.Value;

        var result = await billing_app.SettleWallet(code);

        Assert.Equal(2, result.Code / 100);

        Assert.Equal(BalanceStatus.Canceled, invoice_moq.Data.Single(i => i.Code == code).Status);

        var debit = (await wallet_repo.GetTransactions(2)).Single(w => w.Direction == BalanceDirection.Debit && w.Amount == 180);

        Assert.Equal(BalanceStatus.Completed, debit.Status);
        Assert.Equal(code, debit.InvoiceCode);

        var renewal_moq = scope.ServiceProvider.GetRequiredService<RenewalRepositoryMoq>();
        Assert.Contains(renewal_moq.Data, r => r.WalletDebit == debit.Id);

        Assert.Equal(1320, await wallet_repo.GetBalance(2));

        await Assert.ThrowsAsync<UserException>(() => billing_app.SettleWallet(code));
    }

    [Fact]
    public async Task SettleWallet_InsufficientBalance()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_app = scope.ServiceProvider.GetRequiredService<IBillingApplication>();
        var invoice_moq = scope.ServiceProvider.GetRequiredService<InvoiceRepositoryMoq>();

        job_context.InjectJobContext(3, "User3", "User3");

        var code = (await billing_app.IssuePlanInvoice(500, "User3t|2u|120d|50g", "پلن تست")).Data!.Value;

        await Assert.ThrowsAsync<UserException>(() => billing_app.SettleWallet(code));

        Assert.Equal(BalanceStatus.Pending, invoice_moq.Data.Single(i => i.Code == code).Status);
    }

    [Fact]
    public async Task SettleWallet_RenewalFailure_RevertsToPending()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_app = scope.ServiceProvider.GetRequiredService<IBillingApplication>();
        var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
        var invoice_moq = scope.ServiceProvider.GetRequiredService<InvoiceRepositoryMoq>();

        job_context.InjectJobContext(2, "User2", "User2");

        // 30 gigabytes is not a multiple of 25 → executor validation fails (throws) after the CAS.
        var code = (await billing_app.IssuePlanInvoice(140, "User2t|2u|120d|30g", "پلن تست")).Data!.Value;

        await Assert.ThrowsAsync<UserException>(() => billing_app.SettleWallet(code));

        var invoice = invoice_moq.Data.Single(i => i.Code == code);

        Assert.Equal(BalanceStatus.Pending, invoice.Status);

        Assert.Empty((await wallet_repo.GetTransactions(2)).Where(w => w.Direction == BalanceDirection.Debit));

        Assert.Equal(1500, await wallet_repo.GetBalance(2));

        // After revert the user can fix the request: a valid invoice settles fine.
        var valid_code = (await billing_app.IssuePlanInvoice(180, "User2t|2u|120d|50g", "پلن تست")).Data!.Value;

        var result = await billing_app.SettleWallet(valid_code);

        Assert.Equal(2, result.Code / 100);
    }

    [Fact]
    public async Task SettleWallet_TopUpKind_Rejected()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_app = scope.ServiceProvider.GetRequiredService<IBillingApplication>();
        var invoice_moq = scope.ServiceProvider.GetRequiredService<InvoiceRepositoryMoq>();

        job_context.InjectJobContext(2, "User2", "User2");

        var code = (await billing_app.IssueTopUp(500)).Data!.Value;

        await Assert.ThrowsAsync<UserException>(() => billing_app.SettleWallet(code));

        Assert.Equal(BalanceStatus.Pending, invoice_moq.Data.Single(i => i.Code == code).Status);
    }

    [Fact]
    public async Task SettleWallet_OwnershipByTarget()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_app = scope.ServiceProvider.GetRequiredService<IBillingApplication>();
        var invoice_moq = scope.ServiceProvider.GetRequiredService<InvoiceRepositoryMoq>();

        job_context.InjectJobContext(2, "User2", "User2");

        var code = (await billing_app.IssuePlanInvoice(180, "User2t|2u|120d|50g", "پلن تست")).Data!.Value;

        job_context.InjectJobContext(3, "User3", "User3");

        await Assert.ThrowsAsync<UserException>(() => billing_app.SettleWallet(code));

        Assert.Equal(BalanceStatus.Pending, invoice_moq.Data.Single(i => i.Code == code).Status);
    }

    [Fact]
    public async Task GetInvoice_NotFound()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_app = scope.ServiceProvider.GetRequiredService<IBillingApplication>();

        job_context.InjectJobContext(99, "User99", "User99");

        await Assert.ThrowsAsync<UserException>(() => billing_app.GetInvoice(999999));
    }

    [Fact]
    public async Task RegisterReceipt_OwnershipByTarget()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_app = scope.ServiceProvider.GetRequiredService<IBillingApplication>();
        var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();

        job_context.InjectJobContext(2, "User2", "User2");

        var code = (await billing_app.IssueTopUp(500)).Data!.Value;

        job_context.InjectJobContext(3, "User3", "User3");

        await Assert.ThrowsAsync<UserException>(() =>
            billing_app.RegisterReceipt(code, null, null, null, "stolen receipt"));

        Assert.Empty((await wallet_repo.GetTransactions(3)).Where(w => w.InvoiceCode == code));
        Assert.Empty((await wallet_repo.GetTransactions(2)).Where(w => w.InvoiceCode == code));
    }

    [Fact]
    public async Task RegisterReceipt_NotAwaitingReceipt()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_app = scope.ServiceProvider.GetRequiredService<IBillingApplication>();
        var invoice_repo = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();

        job_context.InjectJobContext(2, "User2", "User2");

        // Plan invoice fully covered by wallet (payable <= 0) must not take a receipt.
        var covered_code = (await billing_app.IssuePlanInvoice(180, "User2t|2u|120d|50g", "پلن تست")).Data!.Value;

        await Assert.ThrowsAsync<UserException>(() =>
            billing_app.RegisterReceipt(covered_code, null, null, null, "receipt"));

        // Already-settled (Canceled) invoice must not take a receipt either.
        var topup_code = (await billing_app.IssueTopUp(500)).Data!.Value;

        await invoice_repo.TransitionStatus(topup_code, BalanceStatus.Pending, BalanceStatus.Canceled);

        await Assert.ThrowsAsync<UserException>(() =>
            billing_app.RegisterReceipt(topup_code, null, null, null, "receipt"));
    }

    [Fact]
    public async Task RegisterReceipt_TextBoundary()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_app = scope.ServiceProvider.GetRequiredService<IBillingApplication>();
        var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();

        job_context.InjectJobContext(99, "User99", "User99");

        var code = (await billing_app.IssueTopUp(500)).Data!.Value;

        var result = await billing_app.RegisterReceipt(code, null, null, null, new string('x', 1000));

        Assert.Equal(2, result.Code / 100);

        Assert.Single((await wallet_repo.GetTransactions(99)).Where(w => w.InvoiceCode == code));
    }

    [Fact]
    public async Task RegisterReceipt_Plan_RenewalFailure_CreditKept()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_app = scope.ServiceProvider.GetRequiredService<IBillingApplication>();
        var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
        var invoice_moq = scope.ServiceProvider.GetRequiredService<InvoiceRepositoryMoq>();

        job_context.InjectJobContext(99, "User99", "User99");

        // Issue with an estimate lower than the real price (180): receipt credit (100) stays
        // below the money-need → renewal throws → credit must be kept as Verifying.
        var code = (await billing_app.IssuePlanInvoice(100, "User99t|2u|120d|50g", "پلن تست")).Data!.Value;

        await Assert.ThrowsAsync<UserException>(() =>
            billing_app.RegisterReceipt(code, null, null, null, "receipt text"));

        var invoice = invoice_moq.Data.Single(i => i.Code == code);
        Assert.Equal(BalanceStatus.Verifying, invoice.Status);

        var credit = (await wallet_repo.GetTransactions(99)).Single(w => w.InvoiceCode == code && w.Direction == BalanceDirection.Credit);
        Assert.Equal(BalanceStatus.Verifying, credit.Status);
        Assert.Equal(100, credit.Amount);

        Assert.Equal(100, await wallet_repo.GetBalance(99));

        Assert.Empty((await wallet_repo.GetTransactions(99)).Where(w => w.Direction == BalanceDirection.Debit));
    }

    [Fact]
    public async Task IssueTopUp_RecordsHistory()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_app = scope.ServiceProvider.GetRequiredService<IBillingApplication>();
        var history_moq = scope.ServiceProvider.GetRequiredService<HistoryRepositoryMoq>();

        job_context.InjectJobContext(99, "User99", "User99");

        await billing_app.IssueTopUp(500);

        var entry = history_moq.Data.SingleOrDefault(h =>
            h.Target == 99 && h.Title == "مالی" && h.Description == "فاکتور صادر شد.");

        Assert.NotNull(entry);
        Assert.Equal(500, entry.Price);
    }
}
