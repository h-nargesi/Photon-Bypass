using PhotonBypass.Application.Billing;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;

namespace PhotonBypass.Test.Facts.LocalDatabase;

public class BillingP2Test : OutSourceLevelServiceInitializer
{
    private const string TestPackage = "BillingP2";

    private static async Task<Dictionary<string, int>> GetTestAccountIds(IServiceProvider provider)
    {
        return await provider.GetRequiredService<IAccountRepository>()
            .GetAccountIdByUsername(["Alpha", "Beta"]);
    }

    private static async Task<InvoiceEntity> InsertInvoice(IInvoiceRepository repo, int account_id,
        InvoiceKind kind = InvoiceKind.TopUp, int total = 700, BalanceStatus status = BalanceStatus.Pending)
    {
        var invoice = new InvoiceEntity
        {
            Code = await repo.GenerateNewInvoiceCode(),
            AccountId = account_id,
            Kind = kind,
            Title = "T2-fresh",
            TotalPrice = total,
            WalletDeduction = 0,
            Payable = total,
            Status = status,
        };

        var inserted = await repo.Insert(invoice);

        Assert.Equal(1, inserted);

        return invoice;
    }

    [Fact]
    public async Task RegisterReceipt_CasAllowsOnlyOne()
    {
        using var scope_a = App.Services.CreateScope();
        await scope_a.Register<LocalDatabaseInitializer>(TestPackage, this);
        using var scope_b = App.Services.CreateScope();
        await scope_b.Register<LocalDatabaseInitializer>(TestPackage, this);

        var repo_a = scope_a.ServiceProvider.GetRequiredService<IInvoiceRepository>();
        var repo_b = scope_b.ServiceProvider.GetRequiredService<IInvoiceRepository>();

        var account_ids = await GetTestAccountIds(scope_a.ServiceProvider);
        var invoice = await InsertInvoice(repo_a, account_ids["Alpha"]);

        var first = repo_a.RegisterReceipt(invoice.Code, null, "first receipt");
        var second = repo_b.RegisterReceipt(invoice.Code, null, "second receipt");

        await Task.WhenAll(first, second);

        Assert.Equal(1, first.Result + second.Result);

        var result = await repo_a.GetByCode(invoice.Code);

        Assert.NotNull(result);
        Assert.Equal(BalanceStatus.Verifying, result.Status);
        Assert.NotNull(result.ReceiptText);
        Assert.Contains(result.ReceiptText, new[] { "first receipt", "second receipt" });
    }

    [Fact]
    public async Task GetBalance_IncludesVerifying()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
        var account_ids = await GetTestAccountIds(scope.ServiceProvider);

        var alpha_balance = await wallet_repo.GetBalance(account_ids["Alpha"]);
        var beta_balance = await wallet_repo.GetBalance(account_ids["Beta"]);

        Assert.Equal(1500, alpha_balance);
        Assert.Equal(-300, beta_balance);
    }

    [Fact]
    public async Task GetAccountIdsBelowThreshold()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
        var account_ids = await GetTestAccountIds(scope.ServiceProvider);

        var below_zero = await wallet_repo.GetAccountIdsBelowThreshold(0);

        Assert.Contains(account_ids["Beta"], below_zero);
        Assert.DoesNotContain(account_ids["Alpha"], below_zero);

        var below_deep = await wallet_repo.GetAccountIdsBelowThreshold(-1000);

        Assert.Empty(below_deep);
    }

    [Fact]
    public async Task TransitionStatus_Once()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var invoice_repo = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
        var account_ids = await GetTestAccountIds(scope.ServiceProvider);

        var invoice = await InsertInvoice(invoice_repo, account_ids["Alpha"]);

        var updated = await invoice_repo.TransitionStatus(invoice.Code, BalanceStatus.Pending, BalanceStatus.Canceled);

        Assert.Equal(1, updated);

        updated = await invoice_repo.TransitionStatus(invoice.Code, BalanceStatus.Pending, BalanceStatus.Canceled);

        Assert.Equal(0, updated);

        var result = await invoice_repo.GetByCode(invoice.Code);

        Assert.NotNull(result);
        Assert.Equal(BalanceStatus.Canceled, result.Status);
    }

    [Fact]
    public async Task TransitionStatus_ParallelOnlyOne()
    {
        using var scope_a = App.Services.CreateScope();
        await scope_a.Register<LocalDatabaseInitializer>(TestPackage, this);
        using var scope_b = App.Services.CreateScope();
        await scope_b.Register<LocalDatabaseInitializer>(TestPackage, this);

        var repo_a = scope_a.ServiceProvider.GetRequiredService<IInvoiceRepository>();
        var repo_b = scope_b.ServiceProvider.GetRequiredService<IInvoiceRepository>();

        var account_ids = await GetTestAccountIds(scope_a.ServiceProvider);
        var invoice = await InsertInvoice(repo_a, account_ids["Alpha"]);

        var first = repo_a.TransitionStatus(invoice.Code, BalanceStatus.Pending, BalanceStatus.Canceled);
        var second = repo_b.TransitionStatus(invoice.Code, BalanceStatus.Pending, BalanceStatus.Canceled);

        await Task.WhenAll(first, second);

        Assert.Equal(1, first.Result + second.Result);

        var result = await repo_a.GetByCode(invoice.Code);

        Assert.NotNull(result);
        Assert.Equal(BalanceStatus.Canceled, result.Status);
    }

    [Fact]
    public async Task GetByCodeOwner_FiltersByAccount()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var invoice_repo = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
        var account_ids = await GetTestAccountIds(scope.ServiceProvider);

        var invoice = await InsertInvoice(invoice_repo, account_ids["Alpha"]);

        var owner_invoice = await invoice_repo.GetByCodeOwner("Alpha", invoice.Code);

        Assert.NotNull(owner_invoice);
        Assert.Equal(account_ids["Alpha"], owner_invoice.AccountId);

        var foreign_invoice = await invoice_repo.GetByCodeOwner("Beta", invoice.Code);

        Assert.Null(foreign_invoice);

        var missing_invoice = await invoice_repo.GetByCodeOwner("Alpha", 999999);

        Assert.Null(missing_invoice);
    }

    [Fact]
    public async Task RegisterReceipt_ImageStored()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var invoice_repo = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
        var account_ids = await GetTestAccountIds(scope.ServiceProvider);

        var invoice = await InsertInvoice(invoice_repo, account_ids["Alpha"]);

        var image = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3 };

        var updated = await invoice_repo.RegisterReceipt(invoice.Code, image, null);

        Assert.Equal(1, updated);

        var result = await invoice_repo.GetByCode(invoice.Code);

        Assert.NotNull(result);
        Assert.Equal(BalanceStatus.Verifying, result.Status);
        Assert.Equal(image, result.ReceiptImage);
        Assert.Null(result.ReceiptText);
        Assert.NotNull(result.ReceiptAt);
        Assert.True(result.HasReceipt);
    }

    [Fact]
    public async Task CancelPreviousPending_LeavesOthers()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var invoice_repo = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
        var account_ids = await GetTestAccountIds(scope.ServiceProvider);
        var alpha_id = account_ids["Alpha"];

        await InsertInvoice(invoice_repo, alpha_id);
        await InsertInvoice(invoice_repo, alpha_id);
        var verifying = await InsertInvoice(invoice_repo, alpha_id, status: BalanceStatus.Verifying);
        var other = await InsertInvoice(invoice_repo, account_ids["Beta"], kind: InvoiceKind.Plan);

        var pending_before = await invoice_repo.GetPendingByAccount(alpha_id);

        var canceled = await invoice_repo.CancelPreviousPending(alpha_id);

        Assert.Equal(pending_before.Count, canceled);

        Assert.Empty(await invoice_repo.GetPendingByAccount(alpha_id));

        var other_invoice = await invoice_repo.GetByCode(other.Code);

        Assert.NotNull(other_invoice);
        Assert.Equal(BalanceStatus.Pending, other_invoice.Status);

        var verifying_invoice = await invoice_repo.GetByCode(verifying.Code);

        Assert.NotNull(verifying_invoice);
        Assert.Equal(BalanceStatus.Verifying, verifying_invoice.Status);
    }

    [Fact]
    public async Task IssueTopUp_CancelsPreviousPendingAppLevel()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_app = scope.ServiceProvider.GetRequiredService<IBillingApplication>();
        var invoice_repo = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
        var account_ids = await GetTestAccountIds(scope.ServiceProvider);

        var alpha_id = account_ids["Alpha"];

        await InsertInvoice(invoice_repo, alpha_id);

        job_context.InjectJobContext(alpha_id, "Alpha", "Alpha");

        var code = await billing_app.IssueTopUp(500);

        Assert.Equal(2, code.Code / 100);
        Assert.NotNull(code.Data);

        var pending = await invoice_repo.GetPendingByAccount(alpha_id);

        var invoice = Assert.Single(pending);

        Assert.Equal(code.Data.Value, invoice.Code);
        Assert.Equal(InvoiceKind.TopUp, invoice.Kind);
        Assert.Equal(500, invoice.TotalPrice);
        Assert.Equal(500, invoice.Payable);
    }

    [Fact]
    public async Task SettleWallet_InsufficientBalance()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var billing_app = scope.ServiceProvider.GetRequiredService<IBillingApplication>();
        var invoice_repo = scope.ServiceProvider.GetRequiredService<IInvoiceRepository>();
        var account_ids = await GetTestAccountIds(scope.ServiceProvider);

        var beta_id = account_ids["Beta"];

        var invoice = await InsertInvoice(invoice_repo, beta_id, InvoiceKind.Plan, 400);

        job_context.InjectJobContext(beta_id, "Beta", "Beta");

        await Assert.ThrowsAsync<UserException>(() => billing_app.SettleWallet(invoice.Code));

        var result = await invoice_repo.GetByCode(invoice.Code);

        Assert.NotNull(result);
        Assert.Equal(BalanceStatus.Pending, result.Status);
    }
}
