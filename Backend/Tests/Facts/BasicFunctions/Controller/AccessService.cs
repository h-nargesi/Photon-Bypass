using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.BasicFunctions.Controller;

public class AccessService : UnitLevelServiceInitializer
{
    [Fact]
    public void CheckAccess_InvalidAccess_NoCache()
    {
        using var scope = App.Services.CreateScope();
        var access_service = scope.ServiceProvider.GetRequiredService<IAccessService>();
        var access = access_service.CheckAccess("user-x", "user-b");
        Assert.False(access);
    }

    [Fact]
    public void CheckAccess_InvalidAccess_NullCache()
    {
        using var scope = App.Services.CreateScope();
        var access_service = scope.ServiceProvider.GetRequiredService<IAccessService>();
        access_service.LoginEvent("user", AccountRole.User, null);
        var access = access_service.CheckAccess("user", "user-b");
        Assert.False(access);
    }

    [Fact]
    public void CheckAccess_InvalidAccess_EmptyCache()
    {
        using var scope = App.Services.CreateScope();
        var access_service = scope.ServiceProvider.GetRequiredService<IAccessService>();
        access_service.LoginEvent("user", AccountRole.User, []);
        var access = access_service.CheckAccess("user", "user-b");
        Assert.False(access);
    }

    [Fact]
    public void CheckAccess_InvalidAccess_WithCache()
    {
        using var scope = App.Services.CreateScope();
        var access_service = scope.ServiceProvider.GetRequiredService<IAccessService>();
        access_service.LoginEvent("user", AccountRole.User, ["subuser1", "subuser2"]);
        var access = access_service.CheckAccess("user", "subuser10");
        Assert.False(access);
    }

    [Fact]
    public void CheckAccess_HasAccess()
    {
        using var scope = App.Services.CreateScope();
        var access_service = scope.ServiceProvider.GetRequiredService<IAccessService>();
        access_service.LoginEvent("user", AccountRole.User, ["subuser1", "subuser2"]);
        var access = access_service.CheckAccess("user", "subuser1");
        Assert.True(access);
    }

    [Fact]
    public void CheckAccess_AdminRole_AlwaysHasAccess()
    {
        using var scope = App.Services.CreateScope();
        var access_service = scope.ServiceProvider.GetRequiredService<IAccessService>();
        access_service.LoginEvent("admin", AccountRole.Admin, []);
        var access = access_service.CheckAccess("admin", "any-stranger");
        Assert.True(access);
    }

    [Fact]
    public void CheckAccess_ManagerRole_NoBypassBeyondTargets()
    {
        using var scope = App.Services.CreateScope();
        var access_service = scope.ServiceProvider.GetRequiredService<IAccessService>();
        access_service.LoginEvent("manager", AccountRole.Manager, ["subuser1"]);
        Assert.True(access_service.CheckAccess("manager", "subuser1"));
        Assert.False(access_service.CheckAccess("manager", "any-stranger"));
    }

    [Fact]
    public void InvalidateTargetArea_RemovesAccessUntilNextLogin()
    {
        using var scope = App.Services.CreateScope();
        var access_service = scope.ServiceProvider.GetRequiredService<IAccessService>();
        access_service.LoginEvent("user", AccountRole.User, ["subuser1"]);
        Assert.True(access_service.CheckAccess("user", "subuser1"));

        access_service.InvalidateTargetArea("user");

        Assert.False(access_service.CheckAccess("user", "subuser1"));

        access_service.LoginEvent("user", AccountRole.User, ["subuser1"]);
        Assert.True(access_service.CheckAccess("user", "subuser1"));
    }
}
