using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Facts.LocalDatabase;

public class AccountRepositoryTest : OutSourceLevelServiceInitializer
{
    [Fact]
    public async Task Insert_Update_FetchSimple()
    {
        using var scope = App.Services.CreateScope();
        await scope.InitializeOutSource<LocalDatabaseInitializer>("DbTest1");

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
            await account_repo.GetAccount(account.Id),
            await account_repo.GetAccount(account.Username),
            await account_repo.GetAccountByEmail(account.Email),
            await account_repo.GetAccountByMobile(account.Mobile),
        };

        foreach (var saved in fetch_list)
        {
            Assert.NotNull(saved);
            Assert.True(saved.Id > 0);
            Assert.Equal(account.Id, saved.Id);
            Assert.Equal(account.Name, saved.Name);
            Assert.Equal(account.Surname, saved.Surname);
            Assert.Equal(account.Email, saved.Email);
            Assert.Equal(account.Password, saved.Password);
        }
        
        account.Name = "Ali2";
        account.Surname = "Mor2";
        account.Password = HashHandler.HashPassword("password-x");
        account.Email = "account@mail.com";
        account.Mobile = "+989120001234";

        await account_repo.Save(account);

        fetch_list =
        [
            await account_repo.GetAccount(account.Id),
            await account_repo.GetAccount(account.Username),
            await account_repo.GetAccountByEmail(account.Email),
            await account_repo.GetAccountByMobile(account.Mobile),
        ];

        foreach (var saved in fetch_list)
        {
            Assert.NotNull(saved);
            Assert.True(saved.Id > 0);
            Assert.Equal(account.Id, saved.Id);
            Assert.Equal(account.Name, saved.Name);
            Assert.Equal(account.Surname, saved.Surname);
            Assert.Equal(account.Email, saved.Email);
            Assert.Equal(account.Password, saved.Password);
        }
    }

    [Fact]
    public async Task GetTargetArea()
    {
        using var scope = App.Services.CreateScope();
        await scope.InitializeOutSource<LocalDatabaseInitializer>("DbTest1");

        var account_repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();

        var account = await account_repo.GetAccount("User1");
        Assert.NotNull(account);

        var targets = (await account_repo.GetTargetArea(account.Id))
            .Select(x => x.Username)
            .ToHashSet();

        Assert.Equal(4, targets.Count);
        Assert.Contains("User11", targets);
        Assert.Contains("User12", targets);
        Assert.Contains("User13", targets);
        Assert.Contains("User14", targets);
    }
}