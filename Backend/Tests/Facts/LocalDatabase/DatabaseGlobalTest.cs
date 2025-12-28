using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Facts.LocalDatabase;

public class DatabaseGlobalTest : OutSourceLevelServiceInitializer
{
    private const string TestPackage = "DbTest1";

    [Fact]
    public async Task ShouldCreateNewTransaction()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var account = new AccountEntity
        {
            Name = "Transaction",
            Surname = "Transaction",
            Password = HashHandler.HashPassword("password"),
            Username = "Transaction",
            VpnPassword = "my-password",
        };

        var account_repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();

        await account_repo.DbContext.BeginTransactionAsync();
        await account_repo.Save(account);
        await account_repo.DbContext.RollbackAsync();

        var saved = await account_repo.GetAccount("U0059");

        Assert.Null(saved);
    }

    [Fact]
    public async Task SetNewIdOnInsert()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var account = new AccountEntity
        {
            Name = "SetNewId",
            Surname = "SetNewId",
            Password = HashHandler.HashPassword("password"),
            Username = "SetNewId",
            VpnPassword = "my-password",
        };

        var account_repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();

        await account_repo.Save(account);

        Assert.True(account.Id > 0);

        var saved = await account_repo.GetAccount(account.Id);

        Assert.NotNull(saved);
        Assert.Equal(account.Username, saved.Username);
    }

    [Fact]
    public async Task SetNewIdOnBulkInsert()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var accounts = new List<AccountEntity>
        {
            new()
            {
                Name = "SetNewId",
                Surname = "SetNewIdBulk1",
                Password = HashHandler.HashPassword("password"),
                Username = "SetNewIdBulk1",
                VpnPassword = "my-password",
            },
            new()
            {
                Name = "SetNewId",
                Surname = "SetNewId",
                Password = HashHandler.HashPassword("password"),
                Username = "SetNewIdBulk2",
                VpnPassword = "my-password",
            }
        };

        var account_repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();

        await account_repo.Save(accounts);

        foreach (var account in accounts)
        {
            Assert.True(account.Id > 0);
        }

        var saveds = await account_repo.GetAccounts(accounts.Select(a => a.Id));

        foreach (var account in accounts)
        {
            Assert.True(saveds.TryGetValue(account.Id, out var saved));
            Assert.Equal(account.Username, saved.Username);
        }
    }
}