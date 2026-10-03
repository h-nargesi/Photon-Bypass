using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;

namespace PhotonBypass.Test.Facts.LocalDatabase;

public class BillingP0Test : OutSourceLevelServiceInitializer
{
    private const string TestPackage = "BillingP0";

    private static async Task<Dictionary<string, int>> GetTestAccountIds(IServiceProvider provider)
    {
        return await provider.GetRequiredService<IAccountRepository>()
            .GetAccountIdByUsername(["Alpha", "Beta"]);
    }

    [Fact]
    public async Task Save_BulkInsideTransaction_RollbackDiscards()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
        var account_ids = await GetTestAccountIds(scope.ServiceProvider);
        var alpha_id = account_ids["Alpha"];

        var before = await wallet_repo.GetTransactions(alpha_id);

        var target = before.Single(w => w.Description == "T4-alpha-1");
        var original_amount = target.Amount;
        target.Amount += 50;

        await wallet_repo.DbContext.BeginTransactionAsync();
        try
        {
            await wallet_repo.Save(new[]
            {
                target,
                new WalletEntity { AccountId = alpha_id, Amount = 111, Direction = BalanceDirection.Credit, Status = BalanceStatus.Pending, Description = "T1-insert-1" },
                new WalletEntity { AccountId = alpha_id, Amount = 222, Direction = BalanceDirection.Credit, Status = BalanceStatus.Pending, Description = "T1-insert-2" },
            });

            await wallet_repo.DbContext.RollbackAsync();
        }
        catch
        {
            try { await wallet_repo.DbContext.RollbackAsync(); } catch { }
            throw;
        }

        var after = await wallet_repo.GetTransactions(alpha_id);

        Assert.Equal(before.Count, after.Count);
        Assert.DoesNotContain(after, w => w.Description == "T1-insert-1");
        Assert.DoesNotContain(after, w => w.Description == "T1-insert-2");

        var reloaded = after.Single(w => w.Id == target.Id);
        Assert.Equal(original_amount, reloaded.Amount);
    }

    [Fact]
    public async Task Delete_BulkInsideTransaction_RollbackRestores()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
        var account_ids = await GetTestAccountIds(scope.ServiceProvider);
        var beta_id = account_ids["Beta"];

        var before = await wallet_repo.GetTransactions(beta_id);

        Assert.NotEmpty(before);

        await wallet_repo.DbContext.BeginTransactionAsync();
        try
        {
            await wallet_repo.Delete(before);

            await wallet_repo.DbContext.RollbackAsync();
        }
        catch
        {
            try { await wallet_repo.DbContext.RollbackAsync(); } catch { }
            throw;
        }

        var after = await wallet_repo.GetTransactions(beta_id);

        Assert.Equal(before.Count, after.Count);
        Assert.Contains(after, w => w.Description == "T4-beta");
    }

    [Fact]
    public async Task GenerateNewInvoiceCode_ParallelDistinct()
    {
        using var scope_a = App.Services.CreateScope();
        await scope_a.Register<LocalDatabaseInitializer>(TestPackage, this);
        using var scope_b = App.Services.CreateScope();
        await scope_b.Register<LocalDatabaseInitializer>(TestPackage, this);

        var repo_a = scope_a.ServiceProvider.GetRequiredService<IWalletRepository>();
        var repo_b = scope_b.ServiceProvider.GetRequiredService<IWalletRepository>();

        var first = repo_a.GenerateNewInvoiceCode();
        var second = repo_b.GenerateNewInvoiceCode();

        await Task.WhenAll(first, second);

        Assert.NotEqual(first.Result, second.Result);
    }

    [Fact]
    public async Task GetWalletsAmount_MultipleIds()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
        var account_ids = await GetTestAccountIds(scope.ServiceProvider);
        var alpha_id = account_ids["Alpha"];

        var credit = new WalletEntity { AccountId = alpha_id, Amount = 111, Direction = BalanceDirection.Credit, Status = BalanceStatus.Completed, Description = "T3-credit" };
        var debit = new WalletEntity { AccountId = alpha_id, Amount = 55, Direction = BalanceDirection.Debit, Status = BalanceStatus.Completed, Description = "T3-debit" };

        await wallet_repo.Save(credit);
        await wallet_repo.Save(debit);

        Assert.True(credit.Id > 0);
        Assert.True(debit.Id > 0);

        var amounts = await wallet_repo.GetWalletsAmount([credit.Id, debit.Id]);

        Assert.Equal(2, amounts.Count);
        Assert.Equal(111, amounts[credit.Id]);
        Assert.Equal(-55, amounts[debit.Id]);

        Assert.Empty(await wallet_repo.GetWalletsAmount([]));
    }

    [Fact]
    public async Task GetInvoice_FiltersByTargetAccount()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
        var account_ids = await GetTestAccountIds(scope.ServiceProvider);

        var alpha_invoice = await wallet_repo.GetInvoice("Alpha", 5001);
        var beta_invoice = await wallet_repo.GetInvoice("Beta", 5001);

        Assert.Equal(2, alpha_invoice.Count);
        Assert.All(alpha_invoice, w => Assert.Equal(account_ids["Alpha"], w.AccountId));

        var beta_wallet = Assert.Single(beta_invoice);
        Assert.Equal(account_ids["Beta"], beta_wallet.AccountId);
        Assert.Equal("T4-beta", beta_wallet.Description);
    }

    [Fact]
    public async Task CompleteInvoice_PendingToCompletedOnce()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();

        var updated = await wallet_repo.CompleteInvoice(5001);

        Assert.Equal(3, updated);

        updated = await wallet_repo.CompleteInvoice(5001);

        Assert.Equal(0, updated);

        var invoice_items = await wallet_repo.GetInvoice(5001);
        Assert.All(invoice_items, i => Assert.Equal(BalanceStatus.Completed, i.Status));
    }
}
