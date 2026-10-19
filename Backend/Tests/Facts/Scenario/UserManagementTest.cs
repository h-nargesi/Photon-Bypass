using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.Scenario;

/// <summary>
/// User list and management endpoints over real HTTP (docs/20 phase 4):
/// role scoping of the list, the creation matrix (manager vs admin), the
/// D9/D13 guards, demote/attach rules and cache invalidation effects. Each
/// test seeds its own manager/subordinate pair (the scenario database is
/// shared by the whole class).
/// </summary>
public class UserManagementTest(ProgramLevelInitializer.Factory factory) : HttpHandler(factory)
{
    private record Fixture(string Manager, string Subordinate, string Inactive);

    private async Task<Fixture> SeedManager(string tag)
    {
        var fixture = new Fixture($"um_mgr{tag}", $"um_sub{tag}", $"um_inactive{tag}");

        using var scope = ServiceProvider.CreateScope();
        var account_repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();

        await InsertAccount(fixture.Manager, UserTypes.AllowSelfService, AccountRole.Manager);

        var manager_id = (await account_repo.GetAccountIdByUsername([fixture.Manager]))[fixture.Manager];

        await InsertAccount(fixture.Subordinate, UserTypes.None, owner_id: manager_id);
        await InsertAccount(fixture.Inactive, UserTypes.None, owner_id: manager_id, is_active: false);

        return fixture;
    }

    [Fact]
    public async Task List_ScopesByRole_SearchAndPaging()
    {
        var fixture = await SeedManager("40");

        // a plain user is refused by the Manager policy
        await Register("um_user43", "65", "Passw0rd1");
        await Login("um_user43", "Passw0rd1");
        await UserList(status: 403);

        // the manager sees exactly its own subordinates — inactive included
        await Login(fixture.Manager, "Passw0rd1");

        var manager_list = await UserList();

        Assert.Equal("2", manager_list["totalCount"]);
        Assert.All(ParseItems(manager_list), row => Assert.Equal(fixture.Manager, row["owner"]));
        Assert.Contains(ParseItems(manager_list), row => row["username"] == fixture.Inactive);
        Assert.DoesNotContain(ParseItems(manager_list), row => row["username"] == "um_user43");

        // search narrows by username fragment
        var search = await UserList(fixture.Inactive);

        Assert.Equal("1", search["totalCount"]);
        Assert.Equal(fixture.Inactive, ParseItems(search).Single()["username"]);

        // beyond the last page is empty but consistent
        var tail = await UserList(page: 2);

        Assert.Equal("2", tail["totalCount"]);
        Assert.Empty(ParseItems(tail));

        await UserList(page: 0, status: 4);

        // the admin sees everyone, itself included
        await Login("admin", "admin");

        var admin_list = await UserList("admin");

        Assert.Contains(ParseItems(admin_list), row => row["username"] == "admin");

        var admin_scoped = await UserList(fixture.Inactive);

        var row = Assert.Single(ParseItems(admin_scoped));
        Assert.Equal(fixture.Manager, row["owner"]);
    }

    [Fact]
    public async Task Create_MatrixAndGuards()
    {
        var fixture = await SeedManager("44");
        await Login(fixture.Manager, "Passw0rd1");

        // manager: forced own subordinate; independent flag respected (D3)
        await UserCreate("um_dep45", "InitPass1");
        await UserCreate("um_ind46", "InitPass2", self_service: true);

        // manager cannot pick role/owner (§3)
        await UserCreate("um_mgrtry47", "InitPass3", role: 1, status: 4);
        await UserCreate("um_owntry48", "InitPass4", owner: fixture.Manager, status: 4);

        await Login("admin", "admin");

        // admin: manager account, and an owned user attached to it
        await UserCreate("um_submgr49", "InitPass5", self_service: true, role: 1);
        await UserCreate("um_owned50", "InitPass6", owner: "um_submgr49");

        // admin cannot create an admin (D9)
        await UserCreate("um_admin51", "InitPass7", role: 2, status: 4);

        // duplicate username hits the unique-index path (409)
        await UserCreate("um_dep45", "InitPass8", status: 409);

        // the initial password is mandatory
        await UserCreate("um_nopass52", "", status: 4);

        using (var scope = ServiceProvider.CreateScope())
        {
            var account_repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();

            var manager = await account_repo.GetAccount(fixture.Manager);
            var dependent = await account_repo.GetAccount("um_dep45");
            var independent = await account_repo.GetAccount("um_ind46");
            var sub_manager = await account_repo.GetAccount("um_submgr49");
            var owned = await account_repo.GetAccount("um_owned50");

            Assert.Equal(manager!.Id, dependent!.Owner);
            Assert.Equal(AccountRole.User, dependent.Role);
            Assert.False(dependent.UserType.HasFlag(UserTypes.AllowSelfService));
            Assert.True(independent!.UserType.HasFlag(UserTypes.AllowSelfService));
            Assert.Equal(AccountRole.Manager, sub_manager!.Role);
            Assert.Equal(sub_manager.Id, owned!.Owner);
        }

        // the created dependent can log in with the initial password (D12)
        await Login("um_dep45", "InitPass1");
    }

    [Fact]
    public async Task SetPassword_Guards()
    {
        var fixture = await SeedManager("53");
        await Login(fixture.Manager, "Passw0rd1");

        await UserSetPassword(fixture.Subordinate, "RenewedPass1");

        await Login(fixture.Subordinate, "RenewedPass1");

        // manager cannot reset outside its area
        await Login(fixture.Manager, "Passw0rd1");
        await Register("um_outside54", "66", "Passw0rd1");
        await UserSetPassword("um_outside54", "WhateverPass1", status: 403);

        // admin can reset anyone but itself
        await Login("admin", "admin");
        await UserSetPassword("admin", "NoSelfReset", status: 403);
        await UserSetPassword("um_outside54", "AdminReset1");

        await Login("um_outside54", "AdminReset1");
    }

    [Fact]
    public async Task SetRole_PromotionDemotionGuards()
    {
        var fixture = await SeedManager("55");

        // role changes are admin-only (policy)
        await Login(fixture.Manager, "Passw0rd1");
        await UserSetRole("um_user56", 0, status: 403);

        await Register("um_user56", "67", "Passw0rd1");
        await Login("admin", "admin");

        // self-change and admin-target are refused (D9)
        await UserSetRole("admin", 0, status: 403);
        await UserSetRole(fixture.Manager, 2, status: 4);

        // promote to manager…
        await UserSetRole("um_user56", 1);

        using (var scope = ServiceProvider.CreateScope())
        {
            var account_repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();

            Assert.Equal(AccountRole.Manager, (await account_repo.GetAccount("um_user56"))!.Role);
        }

        // …demoting the seeded manager while it still has an active subordinate → refused
        await UserSetRole(fixture.Manager, 0, status: 4);

        // after deactivating the subordinate the demotion succeeds (§3 guard)
        await UserSetActive(fixture.Subordinate, false);
        await UserSetRole(fixture.Manager, 0);

        using (var scope = ServiceProvider.CreateScope())
        {
            var account_repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();

            Assert.Equal(AccountRole.User, (await account_repo.GetAccount(fixture.Manager))!.Role);
        }
    }

    [Fact]
    public async Task SetOwner_AttachDetachAndGuards()
    {
        var fixture = await SeedManager("57");
        await Register("um_user58", "68", "Passw0rd1");
        await Login("admin", "admin");

        // the owner must be an active manager; self-ownership and admin
        // targets are refused
        await UserSetOwner("um_user58", "um_user58", status: 4);
        await UserSetOwner("um_user58", "admin", status: 4);
        await UserSetOwner("admin", fixture.Manager, status: 4);

        await UserSetOwner("um_user58", fixture.Manager);

        // attached in the same admin session — the manager can manage it
        // immediately (relationship checked live, not via the login cache)
        await Login(fixture.Manager, "Passw0rd1");
        await UserSetPassword("um_user58", "Attached1");
        await Login("um_user58", "Attached1");

        // detach: the manager loses the subordinate right away
        await Login("admin", "admin");
        await UserSetOwner("um_user58", null);

        await Login(fixture.Manager, "Passw0rd1");
        await UserSetPassword("um_user58", "Detached1", status: 403);

        using (var scope = ServiceProvider.CreateScope())
        {
            var account_repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();

            Assert.Null((await account_repo.GetAccount("um_user58"))!.Owner);
        }
    }

    [Fact]
    public async Task SetSelfService_GatesWalletAccess()
    {
        var fixture = await SeedManager("59");

        // the dependent subordinate is gated out of its own wallet (D6)
        await Login(fixture.Subordinate, "Passw0rd1");
        await WalletHistory(status: 403);

        // the manager flips the flag for its own subordinate…
        await Login(fixture.Manager, "Passw0rd1");
        await UserSetSelfService(fixture.Subordinate, true);

        // …and the gate opens for the account itself
        await Login(fixture.Subordinate, "Passw0rd1");
        var history = await WalletHistory();
        Assert.Equal("0", history["totalCount"]);

        // revoking closes it again
        await Login(fixture.Manager, "Passw0rd1");
        await UserSetSelfService(fixture.Subordinate, false);

        await Login(fixture.Subordinate, "Passw0rd1");
        await WalletHistory(status: 403);

        // outside the manager's area → 403
        await Register("um_user60", "69", "Passw0rd1");
        await Login(fixture.Manager, "Passw0rd1");
        await UserSetSelfService("um_user60", false, status: 403);
    }

    [Fact]
    public async Task SetActive_Guards()
    {
        var fixture = await SeedManager("61");
        await Login(fixture.Manager, "Passw0rd1");

        // deactivating the subordinate blocks its login…
        await UserSetActive(fixture.Subordinate, false);
        await Login(fixture.Subordinate, "Passw0rd1", status: 4);

        // …and it stays visible in the manager's list
        var list = await UserList();
        Assert.Contains(ParseItems(list), row => row["username"] == fixture.Subordinate && row["isActive"] == "False");

        // outside the area → 403; admin self → 403 (D9/D13)
        await Register("um_user62", "59", "Passw0rd1");
        await UserSetActive("um_user62", false, status: 403);

        await Login("admin", "admin");
        await UserSetActive("admin", false, status: 403);

        // the admin reactivates it
        await UserSetActive(fixture.Subordinate, true);
        await Login(fixture.Subordinate, "Passw0rd1");
    }
}
