using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Facts.LocalDatabase;

public class AccountRepositoryTest : OutSourceLevelServiceInitializer
{
    private const string TestPackage1 = "DbTest1";
    private const string TestPackage2 = "DbTest2";

    [Fact]
    public async Task Insert_Update_FetchSimple()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage2, this);

        var account_repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();

        var account = new AccountEntity
        {
            Name = "Ali",
            Surname = "Mor",
            Password = PasswordHasher.Hash("password"),
            Username = "U0059",
            Email = "account@mail.com",
            Mobile = "+989120001234",
            VpnPassword = "my-password",
        };

        await account_repo.Save(account);

        var fetch_list = new List<AccountEntity?>
        {
            await account_repo.GetActiveAccount(account.Id),
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
        account.Password = PasswordHasher.Hash("password-x");
        account.Email = "account@mail.com";
        account.Mobile = "+989120001234";

        await account_repo.Save(account);

        fetch_list =
        [
            await account_repo.GetActiveAccount(account.Id),
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
    public async Task LastCredentialRotateTime_RoundTripsThroughSaveAndFetch()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage2, this);

        var account_repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();

        // the rotation stamp starts null (column default) and survives a full
        // write/read cycle — this is the D4 throttle clock backing store
        var account = new AccountEntity
        {
            Name = "Rotate",
            Surname = "Stamp",
            Password = PasswordHasher.Hash("password"),
            Username = "U0060",
            Email = "rotate-stamp@mail.com",
            Mobile = "+989120001235",
            VpnPassword = "uuid-before-rotation",
        };

        await account_repo.Save(account);

        var fetched = await account_repo.GetAccount(account.Username);
        Assert.NotNull(fetched);
        Assert.Null(fetched.LastCredentialRotateTime);

        var rotate_time = DateTime.Now.AddMinutes(-10);
        account.VpnPassword = "uuid-after-rotation";
        account.LastCredentialRotateTime = rotate_time;

        await account_repo.Save(account);

        fetched = await account_repo.GetAccount(account.Username);
        Assert.NotNull(fetched);
        Assert.Equal("uuid-after-rotation", fetched.VpnPassword);
        Assert.NotNull(fetched.LastCredentialRotateTime);
        // MSSQL datetime keeps ~3.3ms ticks: compare within a small window
        Assert.True((fetched.LastCredentialRotateTime.Value - rotate_time).Duration() < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task GetTargetArea()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage1, this);

        var account_repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();

        var account = await account_repo.GetAccount("User99");
        Assert.NotNull(account);

        var targets = (await account_repo.GetActiveTargetArea(account.Id))
            .Select(x => x.Username)
            .ToHashSet();

        Assert.Equal(4, targets.Count);
        Assert.Contains("User11", targets);
        Assert.Contains("User12", targets);
        Assert.Contains("User13", targets);
        Assert.Contains("User14", targets);
    }

    [Fact]
    public async Task GetAccountId()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage1, this);

        var account_repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();

        var account_ids = await account_repo.GetAccountIdByUsername(["User99"]);
        Assert.NotNull(account_ids);
        Assert.Single(account_ids);
        Assert.Contains("User99", account_ids.Keys);
    }
}