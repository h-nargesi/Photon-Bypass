using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using PhotonBypass.Application.Account;
using PhotonBypass.Application.Account.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockLocalRepository;

namespace PhotonBypass.Test.Facts.Application;

/// <summary>
/// User-management decision matrix (docs/20 phase 4, §3): manager scoping and
/// forcing, D9 invariants (no admin creation/promotion/self-operations), the
/// demote-guard for managers with active subordinates, and owner
/// attach/detach validation including cycles and access-cache invalidation.
/// </summary>
public class UserManagementApplicationTest : UnitLevelServiceInitializer
{
    [Fact]
    public async Task GetList_ScopesManagerToSubordinates_AdminSeesAll()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var account_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
        var application = scope.ServiceProvider.GetRequiredService<IUserManagementApplication>();

        account_moq.Data["User5"].Role = AccountRole.Manager;
        account_moq.Data["User6"].IsActive = false; // inactive subordinates stay visible

        job_context.InjectJobContext(5, "User5", "User5");

        var manager_list = (await application.GetList(null, 1)).Data!;

        Assert.All(manager_list.Items, row => Assert.Equal("User5", row.Owner));
        Assert.Contains(manager_list.Items, row => row.Username == "User6");
        Assert.DoesNotContain(manager_list.Items, row => row.Username == "User99");

        account_moq.Data["User99"].Role = AccountRole.Admin;
        job_context.InjectJobContext(99, "User99", "User99");

        var admin_list = (await application.GetList(null, 1)).Data!;

        Assert.Equal(account_moq.Data.Count, admin_list.TotalCount);
        Assert.Contains(admin_list.Items, row => row.Username == "User99");

        // a plain user gets a friendly 403 (defense in depth behind the policy)
        job_context.InjectJobContext(4, "User4", "User4");

        (await Assert.ThrowsAsync<UserException>(() => application.GetList(null, 1)))
            .HttpCode.Should().Be(403);
    }

    [Fact]
    public async Task Create_ManagerForcesOwnSubordinateUser()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var account_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
        var application = scope.ServiceProvider.GetRequiredService<IUserManagementApplication>();

        account_moq.Data["User5"].Role = AccountRole.Manager;
        job_context.InjectJobContext(5, "User5", "User5");

        AccountEntity? created = null;
        account_moq.OnSave += account => created = account;

        var result = await application.Create(new CreateUserModel
        {
            Username = "new_sub",
            Password = "Passw0rd1",
            Email = "new_sub@gmail.com",
            Mobile = "+989120000001",
            AllowSelfService = true,
        });

        Assert.Equal(2, result.Code / 100);
        Assert.NotNull(created);
        Assert.Equal(5, created!.Owner);
        Assert.Equal(AccountRole.User, created.Role);
        Assert.True(created.UserType.HasFlag(UserTypes.AllowSelfService));

        // a manager cannot pick a role or an owner (docs/20 §3)
        await Assert.ThrowsAsync<UserException>(() => application.Create(new CreateUserModel
        {
            Username = "new_sub2", Password = "Passw0rd1", Email = "n2@gmail.com", Mobile = "+989120000002",
            Role = AccountRole.Manager,
        }));

        await Assert.ThrowsAsync<UserException>(() => application.Create(new CreateUserModel
        {
            Username = "new_sub3", Password = "Passw0rd1", Email = "n3@gmail.com", Mobile = "+989120000003",
            Owner = "User4",
        }));
    }

    [Fact]
    public async Task Create_AdminGuards()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var account_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
        var application = scope.ServiceProvider.GetRequiredService<IUserManagementApplication>();

        account_moq.Data["User99"].Role = AccountRole.Admin;
        account_moq.Data["User5"].Role = AccountRole.Manager;
        job_context.InjectJobContext(99, "User99", "User99");

        // D9: no admin creation, no promotion path
        await Assert.ThrowsAsync<UserException>(() => application.Create(new CreateUserModel
        {
            Username = "new_admin", Password = "Passw0rd1", Email = "na@gmail.com", Mobile = "+989120000010",
            Role = AccountRole.Admin,
        }));

        // owner must be an active manager
        await Assert.ThrowsAsync<UserException>(() => application.Create(new CreateUserModel
        {
            Username = "new_owned", Password = "Passw0rd1", Email = "no@gmail.com", Mobile = "+989120000011",
            Owner = "User4",
        }));

        AccountEntity? created = null;
        account_moq.OnSave += account => created = account;

        var result = await application.Create(new CreateUserModel
        {
            Username = "new_manager",
            Password = "Passw0rd1",
            Email = "nm@gmail.com",
            Mobile = "+989120000012",
            Role = AccountRole.Manager,
            Owner = "User5",
        });

        Assert.Equal(2, result.Code / 100);
        Assert.Equal(AccountRole.Manager, created!.Role);
        Assert.Equal(account_moq.Data["User5"].Id, created.Owner);

        // initial password is mandatory (D12)
        await Assert.ThrowsAsync<UserException>(() => application.Create(new CreateUserModel
        {
            Username = "no_pass", Password = "", Email = "np@gmail.com", Mobile = "+989120000013",
        }));
    }

    [Fact]
    public async Task SetRole_DemoteGuardAndD9()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var account_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
        var application = scope.ServiceProvider.GetRequiredService<IUserManagementApplication>();

        account_moq.Data["User99"].Role = AccountRole.Admin;
        account_moq.Data["User5"].Role = AccountRole.Manager; // owns active User6/User8/User9
        job_context.InjectJobContext(99, "User99", "User99");

        // promoting to Admin is refused (D9)
        await Assert.ThrowsAsync<UserException>(
            () => application.SetRole("User5", AccountRole.Admin));

        // the admin cannot change its own role (D9)
        await Assert.ThrowsAsync<UserException>(
            () => application.SetRole("User99", AccountRole.User));

        // demoting a manager with active subordinates is refused (§3 guard)
        await Assert.ThrowsAsync<UserException>(
            () => application.SetRole("User5", AccountRole.User));

        account_moq.Data["User6"].IsActive = false;
        account_moq.Data["User8"].IsActive = false;
        account_moq.Data["User9"].IsActive = false;

        var result = await application.SetRole("User5", AccountRole.User);

        Assert.Equal(2, result.Code / 100);
        Assert.Equal(AccountRole.User, account_moq.Data["User5"].Role);
    }

    [Fact]
    public async Task SetOwner_AttachDetachAndCycleGuard()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var account_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
        var application = scope.ServiceProvider.GetRequiredService<IUserManagementApplication>();

        account_moq.Data["User99"].Role = AccountRole.Admin;
        account_moq.Data["User5"].Role = AccountRole.Manager;
        job_context.InjectJobContext(99, "User99", "User99");

        // attach: the owner must be an active manager
        var attach = await application.SetOwner("User4", "User5");

        Assert.Equal(2, attach.Code / 100);
        Assert.Equal(account_moq.Data["User5"].Id, account_moq.Data["User4"].Owner);

        // detach
        var detach = await application.SetOwner("User4", null);

        Assert.Equal(2, detach.Code / 100);
        Assert.Null(account_moq.Data["User4"].Owner);

        // self-ownership and non-manager owners are refused
        await Assert.ThrowsAsync<UserException>(() => application.SetOwner("User5", "User5"));
        await Assert.ThrowsAsync<UserException>(() => application.SetOwner("User4", "User2"));

        // cycle: 5 owns 6, attaching 5 under 6 must be refused
        account_moq.Data["User6"].Role = AccountRole.Manager;
        account_moq.Data["User6"].Owner = account_moq.Data["User5"].Id;

        await Assert.ThrowsAsync<UserException>(() => application.SetOwner("User5", "User6"));
    }

    [Fact]
    public async Task Management_Operations_InvalidateTargetArea()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var account_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
        var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
        var application = scope.ServiceProvider.GetRequiredService<IUserManagementApplication>();

        account_moq.Data["User99"].Role = AccountRole.Admin;
        job_context.InjectJobContext(99, "User99", "User99");

        cache.Set("TargetArea|User5", new TargetAreaModel(), TimeSpan.FromHours(1));

        await application.SetRole("User5", AccountRole.Manager);

        Assert.Null(cache.Get("TargetArea|User5"));
    }

    [Fact]
    public async Task SetPasswordAndActive_ManagerSubordinateGuards()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var account_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
        var application = scope.ServiceProvider.GetRequiredService<IUserManagementApplication>();

        account_moq.Data["User5"].Role = AccountRole.Manager; // owns 6/8/9, not User2
        job_context.InjectJobContext(5, "User5", "User5");

        (await Assert.ThrowsAsync<UserException>(() => application.SetPassword("User2", "NewPassword")))
            .HttpCode.Should().Be(403);

        var reset = await application.SetPassword("User6", "NewPassword1");

        Assert.Equal(2, reset.Code / 100);

        await Assert.ThrowsAsync<UserException>(() => application.SetPassword("User6", " "));

        (await Assert.ThrowsAsync<UserException>(() => application.SetActive("User2", false)))
            .HttpCode.Should().Be(403);

        var deactivate = await application.SetActive("User6", false);

        Assert.Equal(2, deactivate.Code / 100);
        Assert.False(account_moq.Data["User6"].IsActive);

        // a manager cannot deactivate itself (not its own subordinate either way)
        (await Assert.ThrowsAsync<UserException>(() => application.SetActive("User5", false)))
            .HttpCode.Should().Be(403);
    }

    [Fact]
    public async Task SetSelfService_FlagMatrix()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var account_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
        var application = scope.ServiceProvider.GetRequiredService<IUserManagementApplication>();

        account_moq.Data["User5"].Role = AccountRole.Manager;
        job_context.InjectJobContext(5, "User5", "User5");

        var grant = await application.SetSelfService("User6", true);

        Assert.Equal(2, grant.Code / 100);
        Assert.True(account_moq.Data["User6"].UserType.HasFlag(UserTypes.AllowSelfService));

        await application.SetSelfService("User6", false);

        Assert.False(account_moq.Data["User6"].UserType.HasFlag(UserTypes.AllowSelfService));

        // outside the subordinate area → 403
        (await Assert.ThrowsAsync<UserException>(() => application.SetSelfService("User2", true)))
            .HttpCode.Should().Be(403);
    }
}
