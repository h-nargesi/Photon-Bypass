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

        var user_repo = scope.ServiceProvider.GetRequiredService<IPanelUserRepository>();
        var vpn_repo = scope.ServiceProvider.GetRequiredService<IVpnAccountRepository>();
        var realm_repo = scope.ServiceProvider.GetRequiredService<IRealmRepository>();

        // VpnAccount.RealmId is NOT NULL with an FK to Realm (Docs/22 D5)
        var realm = new RealmEntity { Name = "SimpleRealm" };
        await realm_repo.Save(realm);

        var user = new PanelUserEntity
        {
            Name = "Ali",
            Surname = "Mor",
            Password = PasswordHasher.Hash("password"),
            Username = "U0059",
            Email = "account@mail.com",
            Mobile = "+989120001234",
        };

        await user_repo.Save(user);

        var vpn_account = new VpnAccountEntity
        {
            UserId = user.Id,
            RealmId = realm.Id,
            Username = user.Username,
            VpnPassword = "my-password",
        };

        await vpn_repo.Save(vpn_account);

        var fetch_list = new List<PanelUserEntity?>
        {
            await user_repo.GetActiveUser(user.Id),
            await user_repo.GetUser(user.Username),
            await user_repo.GetUserByEmail(user.Email),
            await user_repo.GetUserByMobile(user.Mobile),
        };

        foreach (var saved in fetch_list)
        {
            Assert.NotNull(saved);
            Assert.True(saved.Id > 0);
            Assert.Equal(user.Id, saved.Id);
            Assert.Equal(user.Name, saved.Name);
            Assert.Equal(user.Surname, saved.Surname);
            Assert.Equal(user.Email, saved.Email);
            Assert.Equal(user.Password, saved.Password);
        }

        var vpn_fetch_list = new List<VpnAccountEntity?>
        {
            await vpn_repo.GetVisibleAccount(vpn_account.Id),
            await vpn_repo.GetAccount(vpn_account.Username),
        };

        foreach (var saved in vpn_fetch_list)
        {
            Assert.NotNull(saved);
            Assert.True(saved.Id > 0);
            Assert.Equal(vpn_account.Id, saved.Id);
            Assert.Equal(user.Id, saved.UserId);
            Assert.Equal(realm.Id, saved.RealmId);
            Assert.Equal(vpn_account.Username, saved.Username);
            Assert.Equal(vpn_account.VpnPassword, saved.VpnPassword);
        }

        user.Name = "Ali2";
        user.Surname = "Mor2";
        user.Password = PasswordHasher.Hash("password-x");
        user.Email = "account@mail.com";
        user.Mobile = "+989120001234";
        vpn_account.VpnPassword = "my-password-x";

        await user_repo.Save(user);
        await vpn_repo.Save(vpn_account);

        fetch_list =
        [
            await user_repo.GetActiveUser(user.Id),
            await user_repo.GetUser(user.Username),
            await user_repo.GetUserByEmail(user.Email),
            await user_repo.GetUserByMobile(user.Mobile),
        ];

        foreach (var saved in fetch_list)
        {
            Assert.NotNull(saved);
            Assert.True(saved.Id > 0);
            Assert.Equal(user.Id, saved.Id);
            Assert.Equal(user.Name, saved.Name);
            Assert.Equal(user.Surname, saved.Surname);
            Assert.Equal(user.Email, saved.Email);
            Assert.Equal(user.Password, saved.Password);
        }

        vpn_fetch_list =
        [
            await vpn_repo.GetVisibleAccount(vpn_account.Id),
            await vpn_repo.GetAccount(vpn_account.Username),
        ];

        foreach (var saved in vpn_fetch_list)
        {
            Assert.NotNull(saved);
            Assert.True(saved.Id > 0);
            Assert.Equal(vpn_account.Id, saved.Id);
            Assert.Equal(user.Id, saved.UserId);
            Assert.Equal(realm.Id, saved.RealmId);
            Assert.Equal(vpn_account.Username, saved.Username);
            Assert.Equal(vpn_account.VpnPassword, saved.VpnPassword);
        }
    }

    [Fact]
    public async Task LastCredentialRotateTime_RoundTripsThroughSaveAndFetch()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage2, this);

        var user_repo = scope.ServiceProvider.GetRequiredService<IPanelUserRepository>();
        var vpn_repo = scope.ServiceProvider.GetRequiredService<IVpnAccountRepository>();
        var realm_repo = scope.ServiceProvider.GetRequiredService<IRealmRepository>();

        var realm = new RealmEntity { Name = "RotateRealm" };
        await realm_repo.Save(realm);

        var user = new PanelUserEntity
        {
            Name = "Rotate",
            Surname = "Stamp",
            Password = PasswordHasher.Hash("password"),
            Username = "U0060",
            Email = "rotate-stamp@mail.com",
            Mobile = "+989120001235",
        };

        await user_repo.Save(user);

        // the rotation stamp starts null (column default) and survives a full
        // write/read cycle — this is the D4 throttle clock backing store
        var vpn_account = new VpnAccountEntity
        {
            UserId = user.Id,
            RealmId = realm.Id,
            Username = user.Username,
            VpnPassword = "uuid-before-rotation",
        };

        await vpn_repo.Save(vpn_account);

        var fetched = await vpn_repo.GetAccount(vpn_account.Username);
        Assert.NotNull(fetched);
        Assert.Null(fetched.LastCredentialRotateTime);

        var rotate_time = DateTime.Now.AddMinutes(-10);
        vpn_account.VpnPassword = "uuid-after-rotation";
        vpn_account.LastCredentialRotateTime = rotate_time;

        await vpn_repo.Save(vpn_account);

        fetched = await vpn_repo.GetAccount(vpn_account.Username);
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

        var user_repo = scope.ServiceProvider.GetRequiredService<IPanelUserRepository>();

        var user = await user_repo.GetUser("User99");
        Assert.NotNull(user);

        var targets = (await user_repo.GetActiveTargetArea(user.Id))
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

        var vpn_repo = scope.ServiceProvider.GetRequiredService<IVpnAccountRepository>();

        var account_ids = await vpn_repo.GetAccountIdByUsername(["User99"]);
        Assert.NotNull(account_ids);
        Assert.Single(account_ids);
        Assert.Contains("User99", account_ids.Keys);
    }

    [Fact]
    public async Task SearchUsers_ScopeSearchPagingAndPlanJoin()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage2, this);

        var user_repo = scope.ServiceProvider.GetRequiredService<IPanelUserRepository>();
        var vpn_repo = scope.ServiceProvider.GetRequiredService<IVpnAccountRepository>();
        var renewal_repo = scope.ServiceProvider.GetRequiredService<IRenewalRepository>();
        var traffic_repo = scope.ServiceProvider.GetRequiredService<ITrafficDataRepository>();

        var manager = new PanelUserEntity
        {
            Username = "SchMgr",
            Password = PasswordHasher.Hash("password"),
            Email = "sch-mgr@mail.com",
            Mobile = "+989120000081",
        };
        await user_repo.Save(manager);

        var sub_active = new PanelUserEntity
        {
            Username = "SchSubOne",
            Password = PasswordHasher.Hash("password"),
            Owner = manager.Id,
            Email = "sch-sub1@mail.com",
            Mobile = "+989120000082",
        };
        var sub_inactive = new PanelUserEntity
        {
            Username = "SchSubTwo",
            Password = PasswordHasher.Hash("password"),
            Owner = manager.Id,
            Email = "sch-sub2@mail.com",
            Mobile = "+989120000083",
        };
        await user_repo.Save([sub_active, sub_inactive]);
        await user_repo.SetDeactivated([sub_inactive.Id]);

        // Renewal.WalletDebit has an FK to Wallet — seed the debit row first
        var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
        var debit = new WalletEntity
        {
            UserId = sub_active.Id,
            Amount = 100,
            Direction = BalanceDirection.Debit,
            Status = BalanceStatus.Completed,
            Description = "seed debit",
        };
        await wallet_repo.Save(debit);

        // TrafficData.NasId has an FK to Server and VpnAccount.RealmId an FK
        // to Realm — seed realm + NAS first
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

        // one VPN account per subordinate; the inactive user's account mirrors
        // its deactivated state at account level (Docs/22 D12)
        var planned_account = new VpnAccountEntity
        {
            UserId = sub_active.Id,
            RealmId = realm.Id,
            Username = sub_active.Username,
            VpnPassword = "vpn",
        };
        var unplanned_account = new VpnAccountEntity
        {
            UserId = sub_inactive.Id,
            RealmId = realm.Id,
            Username = sub_inactive.Username,
            VpnPassword = "vpn",
        };
        await vpn_repo.Save([planned_account, unplanned_account]);
        await vpn_repo.SetStatus([unplanned_account.Id], VpnAccountStatus.Deactivated);

        await renewal_repo.Save(new RenewalEntity
        {
            VpnAccountId = planned_account.Id,
            SimultaneousUser = 3,
            TrafficLimit = 25,
            TimeLimitInDays = 120,
            // D16: the view reads the stacked expiry stored at purchase time
            ExpirationDate = DateTime.Now.AddDays(120),
            WalletDebit = debit.Id,
        });

        await traffic_repo.Save(new TrafficDataEntity
        {
            VpnAccountId = planned_account.Id,
            NasId = nas.Id,
            SessionId = "sch-session-1",
            StartSession = DateTime.Now.AddHours(-1),
            DataIn = 3,
            DataOut = 2,
        });

        // manager scope: both subordinates, the inactive one included
        var (rows, total) = await user_repo.SearchUsers(manager.Id, null, 1, 20);

        Assert.Equal(2, total);
        Assert.All(rows, row => Assert.Equal("SchMgr", row.Owner));

        Assert.Single(rows, row => row.Username == "SchSubOne");
        var unplanned_row = Assert.Single(rows, row => row.Username == "SchSubTwo");
        Assert.Equal(PanelUserStatus.Deactivated, unplanned_row.Status);

        // the plan summary nests under each VPN account (Docs/22 §3.6)
        var overview = await vpn_repo.GetAccountsOverview(["SchSubOne", "SchSubTwo"]);

        var planned = Assert.Single(overview, row => row.Owner == "SchSubOne");
        Assert.Equal((byte)3, planned.SimultaneousUser);
        Assert.Equal(20, planned.TrafficLeft); // 25G limit - 5G used
        Assert.NotNull(planned.ExpirationDate);

        var unplanned = Assert.Single(overview, row => row.Owner == "SchSubTwo");
        Assert.Null(unplanned.SimultaneousUser);
        Assert.Equal(VpnAccountStatus.Deactivated, unplanned.Status);

        // admin scope + search over username/name
        var (_, admin_total) = await user_repo.SearchUsers(null, "SchSub", 1, 20);
        Assert.Equal(2, admin_total);

        var (search_rows, search_total) = await user_repo.SearchUsers(null, "SchSubOne", 1, 20);
        Assert.Equal(1, search_total);
        Assert.Equal("SchSubOne", search_rows.Single().Username);

        // paging (SchMgr + SchSubOne + SchSubTwo)
        var (page1, paged_total) = await user_repo.SearchUsers(null, "Sch", 1, 2);
        Assert.Equal(3, paged_total);
        Assert.Equal(2, page1.Count);

        var (page2, _) = await user_repo.SearchUsers(null, "Sch", 2, 2);
        Assert.Single(page2);

        // the demote guard counts only active subordinates
        Assert.True(await user_repo.HasActiveSubordinate(manager.Id));

        await user_repo.SetDeactivated([sub_active.Id]);

        Assert.False(await user_repo.HasActiveSubordinate(manager.Id));
    }
}
