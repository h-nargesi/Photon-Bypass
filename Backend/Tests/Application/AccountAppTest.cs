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
    public void GetUser_InvalidUser()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAccountApplication>();

        var action = () => account_app.GetUser("Invalid username").Wait();

        action.Should().Throw<UserException>();
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
    public void GetFullInfo_InvalidUser()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAccountApplication>();

        var action = () => account_app.GetFullInfo("Invalid username").Wait();

        action.Should().Throw<UserException>();
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
    public void EditUser_InvalidUser()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAccountApplication>();

        var action = () => account_app.EditUser("Invalid username", new EditUserContext()).Wait();

        action.Should().Throw<UserException>();
    }
}