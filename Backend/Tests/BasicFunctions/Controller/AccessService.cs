using PhotonBypass.Domain.Account;

namespace PhotonBypass.Test.BasicFunctions.Controller;

public class AccessService : ServiceInitializer
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
        access_service.LoginEvent("user", null!);
        var access = access_service.CheckAccess("user", "user-b");
        Assert.False(access);
    }

    [Fact]
    public void CheckAccess_InvalidAccess_EmptyCache()
    {
        using var scope = App.Services.CreateScope();
        var access_service = scope.ServiceProvider.GetRequiredService<IAccessService>();
        access_service.LoginEvent("user", []);
        var access = access_service.CheckAccess("user", "user-b");
        Assert.False(access);
    }

    [Fact]
    public void CheckAccess_InvalidAccess_WithCache()
    {
        using var scope = App.Services.CreateScope();
        var access_service = scope.ServiceProvider.GetRequiredService<IAccessService>();
        access_service.LoginEvent("user", ["subuser1", "subuser2"]);
        var access = access_service.CheckAccess("user", "subuser10");
        Assert.False(access);
    }

    [Fact]
    public void CheckAccess_HasAccess()
    {
        using var scope = App.Services.CreateScope();
        var access_service = scope.ServiceProvider.GetRequiredService<IAccessService>();
        access_service.LoginEvent("user", ["subuser1", "subuser2"]);
        var access = access_service.CheckAccess("user", "subuser1");
        Assert.True(access);
    }
}