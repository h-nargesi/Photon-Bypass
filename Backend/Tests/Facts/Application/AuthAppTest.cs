using FluentAssertions;
using Microsoft.Extensions.Options;
using PhotonBypass.Application.Authentication;
using PhotonBypass.Application.Management.Model;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockLocalRepository;
using PhotonBypass.Test.Mock.MockServerBridge;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Facts.Application;

public class AuthAppTest : UnitLevelServiceInitializer
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
        var result = await auth_app.CheckUserPassword("User99", "invalid password");

        Assert.NotNull(result);
        Assert.Equal(401, result.Code);
    }

    [Fact]
    public async Task CheckUserPassword_WithTargetArea()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var result = await auth_app.CheckUserPassword("User99", "abc");

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
        Assert.NotNull(result.Data);
        Assert.Equal("User99", result.Data.Username);
        Assert.NotNull(result.Data.TargetArea);
        Assert.Equal(3, result.Data.TargetArea.Count);
        Assert.Equal(AccountRole.User, result.Data.Role);
        Assert.True(result.Data.AllowSelfService);
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
    public async Task ForgetPassword_InvalidMobileEmail()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var function = () => auth_app.ForgetPassword("invalid email/mobile");

        await function.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task ForgetPassword_InvalidAccount_ViaEmail()
    {
        using var scope = App.Services.CreateScope();
        var email_handler_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();

        var is_sent = false;
        email_handler_moq.OnSend += _ => is_sent = true;

        var result = await auth_app.ForgetPassword("ali_moli@diff.com");

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
        Assert.False(is_sent);
    }

    [Fact]
    public async Task ForgetPassword_InactiveAccount_ViaEmail()
    {
        using var scope = App.Services.CreateScope();
        var email_handler_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();

        var is_sent = false;
        email_handler_moq.OnSend += _ => is_sent = true;

        var result = await auth_app.ForgetPassword("user7@gmail.com");

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
        Assert.False(is_sent);
    }

    [Fact]
    public async Task ForgetPassword_SendEmail()
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

        var result = await auth_app.ForgetPassword("user4@gmail.com");

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
            Username = "User99",
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

    [Fact]
    public async Task Register_Success_SetsSelfServiceFlag()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var account_mock = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();

        account_mock.OnSave += account => Assert.True(account.UserType.HasFlag(UserTypes.AllowSelfService));

        var result = await auth_app.Register(new RegisterModel
        {
            Username = "username10",
            Email = null,
            Mobile = "09113456799",
        });

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
    }

    [Fact]
    public async Task Register_DisabledByConfig_RejectedWith403()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<ManagementOptions>>();

        options.Value.AllowRegistration = false;

        var function = () => auth_app.Register(new RegisterModel
        {
            Username = "username10",
            Email = null,
            Mobile = "09113456799",
        });

        (await function.Should().ThrowAsync<UserException>())
            .Which.HttpCode.Should().Be(403);
    }

    [Fact]
    public async Task ResetPassword_UnknownCode_Throws()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();

        var function = () => auth_app.ResetPassword("unknown-code", "NewPassword1");

        await function.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task ResetPassword_ExpiredCode_Throws()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var reset_repo = scope.ServiceProvider.GetRequiredService<IResetPassRepository>();

        await reset_repo.AddHashCode(new ResetPassEntity
        {
            AccountId = 1,
            HashCode = "expired-code",
            ExpireDate = DateTime.Now.AddDays(-1),
        });

        var function = () => auth_app.ResetPassword("expired-code", "NewPassword1");

        await function.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task ResetPassword_ValidCode_ChangesPassword()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var account_repo_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
        var reset_repo = scope.ServiceProvider.GetRequiredService<IResetPassRepository>();

        var account_id = account_repo_moq.Data["User99"].Id;

        await reset_repo.AddHashCode(new ResetPassEntity
        {
            AccountId = account_id,
            HashCode = "valid-code",
            ExpireDate = DateTime.Now.AddMinutes(10),
        });

        var result = await auth_app.ResetPassword("valid-code", "NewPassword1");

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
        PasswordHasher.Verify("NewPassword1", account_repo_moq.Data["User99"].Password)
            .Should().BeTrue();
    }
}
