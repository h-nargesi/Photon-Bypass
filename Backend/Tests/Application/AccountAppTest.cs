using FluentAssertions;
using PhotonBypass.Application.Account;
using PhotonBypass.Application.Account.Model;
using PhotonBypass.ErrorHandler;

namespace PhotonBypass.Test.Application;

public class AccountAppTest : ServiceInitializer
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

        var action = () => account_app.GetUser("Invalid username");

        await action.Should().ThrowAsync<UserException>();
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

        var action = () => account_app.GetFullInfo("Invalid username");

        await action.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task EditUser_ValidUser()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAccountApplication>();
        var result = await account_app.EditUser("User1", new EditUserContext
        {
            Firstname = nameof(EditUserContext.Firstname),
            Lastname = nameof(EditUserContext.Lastname),
            Email = nameof(EditUserContext.Email),
            Mobile = nameof(EditUserContext.Mobile),
        });

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
    }

    [Fact]
    public async Task EditUser_InvalidUser()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAccountApplication>();

        var action = () => account_app.EditUser("Invalid username", new EditUserContext());

        await action.Should().ThrowAsync<UserException>();
    }
}