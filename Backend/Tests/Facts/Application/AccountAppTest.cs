using FluentAssertions;
using PhotonBypass.Application.Account;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.Application;

public class AccountAppTest : UnitLevelServiceInitializer
{
    [Fact]
    public async Task GetUser_ValidUser()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAccountApplication>();
        var result = await account_app.GetUser("User1");

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
        Assert.NotNull(result.Data);
        Assert.Equal("User1", result.Data.Username);
    }

    [Fact]
    public async Task GetUser_InvalidUser()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAccountApplication>();

        var func = () => account_app.GetUser("Invalid username");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task GetFullInfo_ValidUser()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAccountApplication>();
        var result = await account_app.GetFullInfo("User1");

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
        Assert.NotNull(result.Data);
        Assert.Equal("User1", result.Data.Username);
    }

    [Fact]
    public async Task GetFullInfo_InvalidUser()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAccountApplication>();

        var func = () => account_app.GetFullInfo("Invalid username");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task EditUser_ValidUser()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAccountApplication>();
        var result = await account_app.EditUser("User1", new EditUserModel
        {
            Firstname = nameof(EditUserModel.Firstname),
            Lastname = nameof(EditUserModel.Lastname),
            Email = "mail@google.com",
            Mobile = "+989315735625",
        });

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
    }

    [Fact]
    public async Task EditUser_InactiveUser()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAccountApplication>();
        var func = () => account_app.EditUser("InactiveUser7", new EditUserModel
        {
            Firstname = nameof(EditUserModel.Firstname),
            Lastname = nameof(EditUserModel.Lastname),
            Email = nameof(EditUserModel.Email),
            Mobile = nameof(EditUserModel.Mobile),
        });

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task EditUser_InvalidUser()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAccountApplication>();

        var func = () => account_app.EditUser("Invalid username", new EditUserModel());

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task ChangePassword_UnknownAccount()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAccountApplication>();

        var func = () => account_app.ChangePassword("Invalid username", string.Empty, string.Empty);

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task ChangePassword_InactiveAccount()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAccountApplication>();

        var func = () => account_app.ChangePassword("InactiveUser7", string.Empty, string.Empty);

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task ChangePassword_BadPassword()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAccountApplication>();

        var func = () => account_app.ChangePassword("User1", "xyz", "new-password");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task ChangePassword_OK()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAccountApplication>();

        var result = await account_app.ChangePassword("User1", "abc", "new-password");

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
    }
}