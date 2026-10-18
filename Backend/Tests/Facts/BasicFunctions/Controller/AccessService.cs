using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.BasicFunctions.Controller;

public class AccessService : UnitLevelServiceInitializer
{
    [Fact]
    public async Task CheckAccess_UnknownAccount_NoCache()
    {
        using var scope = App.Services.CreateScope();
        var access_service = scope.ServiceProvider.GetRequiredService<IAccessService>();
        var access = await access_service.CheckAccess("user-x", "user-b");
        Assert.False(access);
    }

    [Fact]
    public async Task CheckAccess_InvalidAccess_NullCache()
    {
        using var scope = App.Services.CreateScope();
        var access_service = scope.ServiceProvider.GetRequiredService<IAccessService>();
        access_service.LoginEvent("user", AccountRole.User, null);
        var access = await access_service.CheckAccess("user", "user-b");
        Assert.False(access);
    }

    [Fact]
    public async Task CheckAccess_InvalidAccess_EmptyCache()
    {
        using var scope = App.Services.CreateScope();
        var access_service = scope.ServiceProvider.GetRequiredService<IAccessService>();
        access_service.LoginEvent("user", AccountRole.User, []);
        var access = await access_service.CheckAccess("user", "user-b");
        Assert.False(access);
    }

    [Fact]
    public async Task CheckAccess_InvalidAccess_WithCache()
    {
        using var scope = App.Services.CreateScope();
        var access_service = scope.ServiceProvider.GetRequiredService<IAccessService>();
        access_service.LoginEvent("user", AccountRole.User, ["subuser1", "subuser2"]);
        var access = await access_service.CheckAccess("user", "subuser10");
        Assert.False(access);
    }

    [Fact]
    public async Task CheckAccess_HasAccess()
    {
        using var scope = App.Services.CreateScope();
        var access_service = scope.ServiceProvider.GetRequiredService<IAccessService>();
        access_service.LoginEvent("user", AccountRole.User, ["subuser1", "subuser2"]);
        var access = await access_service.CheckAccess("user", "subuser1");
        Assert.True(access);
    }

    [Fact]
    public async Task CheckAccess_AdminRole_AlwaysHasAccess()
    {
        using var scope = App.Services.CreateScope();
        var access_service = scope.ServiceProvider.GetRequiredService<IAccessService>();
        access_service.LoginEvent("admin", AccountRole.Admin, []);
        var access = await access_service.CheckAccess("admin", "any-stranger");
        Assert.True(access);
    }

    [Fact]
    public async Task CheckAccess_ManagerRole_NoBypassBeyondTargets()
    {
        using var scope = App.Services.CreateScope();
        var access_service = scope.ServiceProvider.GetRequiredService<IAccessService>();
        access_service.LoginEvent("manager", AccountRole.Manager, ["subuser1"]);
        Assert.True(await access_service.CheckAccess("manager", "subuser1"));
        Assert.False(await access_service.CheckAccess("manager", "any-stranger"));
    }

    [Fact]
    public async Task InvalidateTargetArea_RemovesAccessUntilNextLogin()
    {
        using var scope = App.Services.CreateScope();
        var access_service = scope.ServiceProvider.GetRequiredService<IAccessService>();
        access_service.LoginEvent("user", AccountRole.User, ["subuser1"]);
        Assert.True(await access_service.CheckAccess("user", "subuser1"));

        access_service.InvalidateTargetArea("user");

        // "user" is not a seeded account, so the rebuild-on-miss keeps this
        // denied — a seeded account would be rebuilt instead (see below)
        Assert.False(await access_service.CheckAccess("user", "subuser1"));

        access_service.LoginEvent("user", AccountRole.User, ["subuser1"]);
        Assert.True(await access_service.CheckAccess("user", "subuser1"));
    }

    [Fact]
    public async Task CheckAccess_CacheMiss_RebuildsAreaFromRepository()
    {
        using var scope = App.Services.CreateScope();
        var access_service = scope.ServiceProvider.GetRequiredService<IAccessService>();

        // no LoginEvent at all: this is the restart / second-instance path
        // (docs/14 M32). User5 owns the seeded subordinates 6/8/9, so the
        // rebuilt area must grant those targets and nothing else
        Assert.True(await access_service.CheckAccess("User5", "User6"));
        Assert.False(await access_service.CheckAccess("User5", "User2"));

        // the rebuilt area lands back in the cache: the second check must not
        // need the repository again
        Assert.True(await access_service.CheckAccess("User5", "User8"));
    }
}
