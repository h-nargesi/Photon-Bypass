using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Facts.LocalDatabase;

public class AccountTest : OutSourceLevelServiceInitializer
{
    [Fact]
    public async Task ShouldCreateNewTransaction()
    {
        using var scope = App.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OutSourceManager>()
            .InitializeOutSource<LocalDatabaseInitializer>(scope, "DbTest1");

        var account = new AccountEntity
        {
            Balance = 0,
            Name = "Ali",
            Surname = "Mor",
            Password = HashHandler.HashPassword("password"),
            Username = "U0059",
            Email = "account@mail.com",
            Mobile = "+989120001234",
            VpnPassword = "my-password",
        };

        var account_repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();

        await account_repo.Save(account);

        var fetch_list = new List<AccountEntity?>
        {
            await account_repo.GetAccount(account.Username),
            await account_repo.GetAccountByEmail(account.Email),
            await account_repo.GetAccountByMobile(account.Mobile),
        };

        foreach (var saved in fetch_list)
        {
            Assert.NotNull(saved);
            Assert.True(saved.Id > 0);
            Assert.Equal(account.Name, saved.Name);
            Assert.Equal(account.Surname, saved.Surname);
            Assert.Equal(account.Email, saved.Email);
            Assert.Equal(account.Password, saved.Password);
        }
    }
}