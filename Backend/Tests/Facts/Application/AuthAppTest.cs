using FluentAssertions;
using Microsoft.Extensions.Options;
using PhotonBypass.Application.Authentication;
using PhotonBypass.Application.Management.Model;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.ErrorHandler;
using PhotonBypass.ServerBridge.Services;
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
            Assert.Equal("user4@gmail.com", message.ToAddress);
        };

        var result = await auth_app.ForgetPassword("user4@gmail.com");

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
    }

    [Fact]
    public async Task ForgetPassword_FourthRequestWithinTheHour_ReturnsNeutralMessageWithoutSending()
    {
        using var scope = App.Services.CreateScope();
        var email_handler_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();

        var send_count = 0;
        email_handler_moq.OnSend += _ => send_count++;

        for (var request = 1; request <= 3; request++)
        {
            var result = await auth_app.ForgetPassword("user4@gmail.com");

            Assert.Equal(2, result.Code / 100);
        }

        Assert.Equal(3, send_count);

        var fourth = await auth_app.ForgetPassword("user4@gmail.com");

        // the limit is invisible to the caller: same neutral 2xx answer
        Assert.NotNull(fourth);
        Assert.Equal(2, fourth.Code / 100);
        Assert.Equal(3, send_count);
    }

    [Fact]
    public async Task ForgetPassword_RateLimit_WorksPerEmailAddress()
    {
        using var scope = App.Services.CreateScope();
        var email_handler_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();

        var send_count = 0;
        email_handler_moq.OnSend += _ => send_count++;

        for (var request = 0; request < 3; request++)
        {
            await auth_app.ForgetPassword("user4@gmail.com");
        }

        // another address has its own window: this one still goes through
        var other = await auth_app.ForgetPassword("user5@gmail.com");

        Assert.Equal(2, other.Code / 100);
        Assert.Equal(4, send_count);
    }

    [Fact]
    public async Task ForgetPassword_UnknownAccount_ConsumesRateLimitSlots()
    {
        using var scope = App.Services.CreateScope();
        var email_handler_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var account_repo_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();

        var send_count = 0;
        email_handler_moq.OnSend += _ => send_count++;

        // three requests for an address that has no account: each consumes a
        // slot BEFORE the lookup, so the limiter cannot be used to tell
        // whether the account exists
        for (var request = 0; request < 3; request++)
        {
            var result = await auth_app.ForgetPassword("rate.user@gmail.com");

            Assert.Equal(2, result.Code / 100);
        }

        send_count.Should().Be(0);

        // the account appears (registered meanwhile) but its window is spent
        account_repo_moq.Data["RateUser"] = new AccountEntity
        {
            Id = 3000,
            Username = "RateUser",
            Email = "rate.user@gmail.com",
            Status = AccountStatus.Active,
        };

        var fourth = await auth_app.ForgetPassword("rate.user@gmail.com");

        fourth.Code.Should().Be(200);
        send_count.Should().Be(0);
    }

    [Fact]
    public async Task ForgetPassword_NewRequest_ExpiresThePreviousCodes()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var reset_moq = scope.ServiceProvider.GetRequiredService<ResetPassRepositoryMoq>();

        var codes = new List<string>();
        var expired_accounts = new List<int>();
        reset_moq.OnAddHashCode += entity => codes.Add(entity.HashCode);
        reset_moq.OnExpireActiveCodes += expired_accounts.Add;

        await auth_app.ForgetPassword("user4@gmail.com");
        await auth_app.ForgetPassword("user4@gmail.com");

        codes.Should().HaveCount(2);
        expired_accounts.Should().AllBeEquivalentTo(4);

        // the first code exists but is expired: only the newest one works
        var first_code = await reset_moq.Object.GetAccount(codes[0]);

        first_code.Should().NotBeNull();
        first_code!.ExpireDate.Should().BeOnOrBefore(DateTime.Now);
    }

    [Fact]
    public async Task ForgetPassword_EmailFailure_ThrowsUserExceptionButKeepsTheCode()
    {
        using var scope = App.Services.CreateScope();
        var email_handler_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var reset_moq = scope.ServiceProvider.GetRequiredService<ResetPassRepositoryMoq>();

        var code = string.Empty;
        reset_moq.OnAddHashCode += entity => code = entity.HashCode;

        email_handler_moq.OnSend += _ => throw new InvalidOperationException("smtp unavailable");

        var function = () => auth_app.ForgetPassword("user4@gmail.com");

        (await function.Should().ThrowAsync<UserException>())
            .Which.UserMessage.Should().Contain("ارسال ایمیل ناموفق بود");

        code.Should().NotBeNullOrEmpty("the stored code must survive a failed send");

        var stored = await reset_moq.Object.GetAccount(code);

        stored.Should().NotBeNull();
        stored!.ExpireDate.Should().BeAfter(DateTime.Now);
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
            Password = "Passw0rd1",
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
            Password = "Passw0rd1",
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
    public async Task Register_WithEmail_SendsWelcomeEmail()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var email_handler_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();

        EmailMessage? sent = null;
        email_handler_moq.OnSend += mail => sent = mail;

        var result = await auth_app.Register(new RegisterModel
        {
            Username = "username10",
            Email = "welcome.user@gmail.com",
            Mobile = "09113456799",
            Password = "Passw0rd1",
        });

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);

        sent.Should().NotBeNull("a registration with an email must send the welcome mail");
        sent!.ToAddress.Should().Be("welcome.user@gmail.com");
        sent.Subject.Should().Contain("خوش آمدید");
        sent.HtmlBody.Should().Contain("username10");
        sent.PlainTextBody.Should().Contain("username10");
    }

    [Fact]
    public async Task Register_WithoutEmail_SendsNoWelcomeEmail()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var email_handler_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();

        var send_count = 0;
        email_handler_moq.OnSend += _ => send_count++;

        var result = await auth_app.Register(new RegisterModel
        {
            Username = "username10",
            Email = null,
            Mobile = "09113456799",
            Password = "Passw0rd1",
        });

        Assert.Equal(2, result.Code / 100);
        send_count.Should().Be(0);
    }

    [Fact]
    public async Task Register_WelcomeEmailFailure_DoesNotFailTheRegistration()
    {
        using var scope = App.Services.CreateScope();
        var auth_app = scope.ServiceProvider.GetRequiredService<IAuthApplication>();
        var email_handler_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();

        email_handler_moq.OnSend += _ => throw new InvalidOperationException("smtp unavailable");

        var result = await auth_app.Register(new RegisterModel
        {
            Username = "username10",
            Email = "welcome.user@gmail.com",
            Mobile = "09113456799",
            Password = "Passw0rd1",
        });

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
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
