using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Facts.LocalDatabase;

public class TransactionTest : OutSourceLevelServiceInitializer
{
    private const string TestPackage = "DbTest1";

    [Fact]
    public async Task ShouldCreateNewTransaction()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var account = new AccountEntity
        {
            Balance = 0,
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

        var saved = await scope.ServiceProvider.GetRequiredService<IAccountRepository>()
            .GetAccount("U0059");

        Assert.Null(saved);
    }
}