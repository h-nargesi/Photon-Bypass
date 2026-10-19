using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;
using PhotonBypass.Tools;
using WalletEntity = PhotonBypass.Domain.Account.Entity.WalletEntity;

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

    [Fact]
    public async Task SearchAccounts_ScopeSearchPagingAndPlanJoin()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage2, this);

        var account_repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var renewal_repo = scope.ServiceProvider.GetRequiredService<IRenewalRepository>();
        var traffic_repo = scope.ServiceProvider.GetRequiredService<ITrafficDataRepository>();

        var manager = new AccountEntity
        {
            Username = "SchMgr",
            Password = PasswordHasher.Hash("password"),
            VpnPassword = "vpn",
            Email = "sch-mgr@mail.com",
            Mobile = "+989120000081",
        };
        await account_repo.Save(manager);

        var sub_active = new AccountEntity
        {
            Username = "SchSubOne",
            Password = PasswordHasher.Hash("password"),
            VpnPassword = "vpn",
            Owner = manager.Id,
            Email = "sch-sub1@mail.com",
            Mobile = "+989120000082",
        };
        var sub_inactive = new AccountEntity
        {
            Username = "SchSubTwo",
            Password = PasswordHasher.Hash("password"),
            VpnPassword = "vpn",
            Owner = manager.Id,
            Email = "sch-sub2@mail.com",
            Mobile = "+989120000083",
        };
        await account_repo.Save([sub_active, sub_inactive]);
        await account_repo.SetDeactivated([sub_inactive.Id]);

        // Renewal.WalletDebit has an FK to Wallet — seed the debit row first
        var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
        var debit = new WalletEntity
        {
            AccountId = sub_active.Id,
            Amount = 100,
            Direction = BalanceDirection.Debit,
            Status = BalanceStatus.Completed,
            Description = "seed debit",
        };
        await wallet_repo.Save(debit);

        await renewal_repo.Save(new RenewalEntity
        {
            AccountId = sub_active.Id,
            SimultaneousUser = 3,
            TrafficLimit = 25,
            TimeLimitInDays = 120,
            // D16: the view reads the stacked expiry stored at purchase time
            ExpirationDate = DateTime.Now.AddDays(120),
            WalletDebit = debit.Id,
        });

        // TrafficData.NasId has an FK to Server — seed realm + NAS first
        var realm_repo = scope.ServiceProvider.GetRequiredService<IRealmRepository>();
        var realm = new RealmEntity { Name = "SchRealm" };
        await realm_repo.Save(realm);

        var server_repo = scope.ServiceProvider.GetRequiredService<IServerRepository>();
        var nas = new ServerEntity
        {
            RealmId = realm.Id,
            IpAddress = "10.10.10.10",
            Name = "SchNas",
            DomainName = "schnas",
            BandWidth = 1_073_741_824,
        };
        await server_repo.Save(nas);

        await traffic_repo.Save(new TrafficDataEntity
        {
            AccountId = sub_active.Id,
            NasId = nas.Id,
            SessionId = "sch-session-1",
            StartSession = DateTime.Now.AddHours(-1),
            DataIn = 3,
            DataOut = 2,
        });

        // manager scope: both subordinates, the inactive one included
        var (rows, total) = await account_repo.SearchAccounts(manager.Id, null, 1, 20);

        Assert.Equal(2, total);
        Assert.All(rows, row => Assert.Equal("SchMgr", row.Owner));

        var planned = Assert.Single(rows, row => row.Username == "SchSubOne");
        Assert.Equal((byte)3, planned.SimultaneousUser);
        Assert.Equal(20, planned.TrafficLeft); // 25G limit - 5G used
        Assert.NotNull(planned.ExpirationDate);

        var unplanned = Assert.Single(rows, row => row.Username == "SchSubTwo");
        Assert.Null(unplanned.SimultaneousUser);
        Assert.Equal(AccountStatus.Deactivated, unplanned.Status);

        // admin scope + search over username/name
        var (_, admin_total) = await account_repo.SearchAccounts(null, "SchSub", 1, 20);
        Assert.Equal(2, admin_total);

        var (search_rows, search_total) = await account_repo.SearchAccounts(null, "SchSubOne", 1, 20);
        Assert.Equal(1, search_total);
        Assert.Equal("SchSubOne", search_rows.Single().Username);

        // paging (SchMgr + SchSubOne + SchSubTwo)
        var (page1, paged_total) = await account_repo.SearchAccounts(null, "Sch", 1, 2);
        Assert.Equal(3, paged_total);
        Assert.Equal(2, page1.Count);

        var (page2, _) = await account_repo.SearchAccounts(null, "Sch", 2, 2);
        Assert.Single(page2);

        // the demote guard counts only active subordinates
        Assert.True(await account_repo.HasActiveSubordinate(manager.Id));

        await account_repo.SetDeactivated([sub_active.Id]);

        Assert.False(await account_repo.HasActiveSubordinate(manager.Id));
    }
}