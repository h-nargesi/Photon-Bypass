using PhotonBypass.Application.Authentication;

namespace PhotonBypass.Test.Application;

public class AuthAppTest : ServiceInitializer
{
    [Fact]
    public async Task CheckUserPassword_InvlidAccount()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var result = await account_app.CheckUserPassword("Invalid User", "some password");

        Assert.NotNull(result);
        Assert.Equal(401, result.Code);
    }

    [Fact]
    public async Task CheckUserPassword_InactiveAccount()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var result = await account_app.CheckUserPassword("InactiveUser7", "some password");

        Assert.NotNull(result);
        Assert.Equal(401, result.Code);
    }

    [Fact]
    public async Task CheckUserPassword_InvlidPassword()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var result = await account_app.CheckUserPassword("User1", "invalid password");

        Assert.NotNull(result);
        Assert.Equal(401, result.Code);
    }

    [Fact]
    public async Task CheckUserPassword_WithTargetArea()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var result = await account_app.CheckUserPassword("User1", "abc");

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
        Assert.NotNull(result.Data);
        Assert.Equal("User1", result.Data.Username);
        Assert.NotNull(result.Data.TargetArea);
        Assert.Equal(2, result.Data.TargetArea.Count);
    }

    [Fact]
    public async Task CheckUserPassword_WithoutTargetArea()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var result = await account_app.CheckUserPassword("User4", "abc");

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
        Assert.NotNull(result.Data);
        Assert.Equal("User4", result.Data.Username);
        Assert.NotNull(result.Data.TargetArea);
        Assert.Empty(result.Data.TargetArea);
    }
}
