using FluentAssertions;
using PhotonBypass.Application.Authentication;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Test.MockOutSources;
using PhotonBypass.Test.MockServerBridge;

namespace PhotonBypass.Test.Application;

public class AuthAppTest : ServiceInitializer
{
    [Fact]
    public async Task CheckUserPassword_InvalidAccount()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var result = await auth_app.CheckUserPassword("Invalid User", "some password");

        Assert.NotNull(result);
        Assert.Equal(401, result.Code);
    }

    [Fact]
    public async Task CheckUserPassword_InactiveAccount()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var result = await auth_app.CheckUserPassword("InactiveUser7", "some password");

        Assert.NotNull(result);
        Assert.Equal(401, result.Code);
    }

    [Fact]
    public async Task CheckUserPassword_InvalidPassword()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var result = await auth_app.CheckUserPassword("User1", "invalid password");

        Assert.NotNull(result);
        Assert.Equal(401, result.Code);
    }

    [Fact]
    public async Task CheckUserPassword_WithTargetArea()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var result = await auth_app.CheckUserPassword("User1", "abc");

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
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var result = await auth_app.CheckUserPassword("User4", "abc");

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
        Assert.NotNull(result.Data);
        Assert.Equal("User4", result.Data.Username);
        Assert.NotNull(result.Data.TargetArea);
        Assert.Empty(result.Data.TargetArea);
    }

    [Fact]
    public async Task ResetPassword_InvalidMobileEmail()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var function = () => auth_app.ResetPassword("invalid email/mobile");

        await function.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task ResetPassword_InvalidAccount_ViaEmail()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var function = () => auth_app.ResetPassword("ali_moli@diff.com");

        await function.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task ResetPassword_InactiveAccount_ViaEmail()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var function = () => auth_app.ResetPassword("user7@gmail.com");

        await function.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task ResetPassword_SendEmail()
    {
        using var scope = App.Services.CreateScope();
        var email_handler_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();

        email_handler_moq.OnSend += message =>
        {
            Assert.NotNull(message);
            Assert.True(message.To.Count > 0);
            Assert.Equal("user4@gmail.com", message.To.First().Address);
        };
        
        var result = await auth_app.ResetPassword("user4@gmail.com");

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
    }

    [Fact]
    public async Task Register_EmptyUsername()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var function = () => auth_app.Register(new RegisterModel
        {
            Email = "test.email.com",
            Mobile = "09123456789",
        });

        await function.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task Register_UsernamePattern()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var function = () => auth_app.Register(new RegisterModel
        {
            Username = "invalid username",
            Email = "test.email.com",
            Mobile = "09123456789",
        });

        await function.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task Register_EmptyMobileAndEmail()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var function = () => auth_app.Register(new RegisterModel
        {
            Username = "username10",
            Email = null,
            Mobile = null,
        });

        await function.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task Register_EmailPattern()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var function = () => auth_app.Register(new RegisterModel
        {
            Username = "username10",
            Email = "invalid email address",
            Mobile = null,
        });

        await function.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task Register_MobileNumberPattern()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var function = () => auth_app.Register(new RegisterModel
        {
            Username = "username10",
            Email = null,
            Mobile = "invalid mobile number",
        });

        await function.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task Register_DuplicatedUsername()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var function = () => auth_app.Register(new RegisterModel
        {
            Username = "User1",
            Email = null,
            Mobile = "09123456789",
        });

        await function.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task Register_DuplicatedEmail()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var function = () => auth_app.Register(new RegisterModel
        {
            Username = "username10",
            Email = "user1@gmail.com",
            Mobile = null,
        });

        await function.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task Register_DuplicatedMobile()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var function = () => auth_app.Register(new RegisterModel
        {
            Username = "username10",
            Email = null,
            Mobile = "09113456789",
        });

        await function.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task Register_Success()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var account_mock = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();

        account_mock.OnSave += (account) => Assert.Equal("+989113456799", account.Mobile);

        var result = await auth_app.Register(new RegisterModel
        {
            Username = "username10",
            Email = null,
            Mobile = "09113456799",
        });

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
    }
}
