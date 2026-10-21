using FluentAssertions;
using PhotonBypass.Application.Account;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockLocalRepository;

namespace PhotonBypass.Test.Facts.Application;

public class AccountAppTest : UnitLevelServiceInitializer
{
    [Fact]
    public async Task GetUser_ValidUser()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAccountApplication>();
        var result = await account_app.GetUser("User99");

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
        Assert.NotNull(result.Data);
        Assert.Equal("User99", result.Data.Username);
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
        var result = await account_app.GetFullInfo("User99");

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
        Assert.NotNull(result.Data);
        Assert.Equal("User99", result.Data.Username);
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
        var result = await account_app.EditUser("User99", new EditUserModel
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

        var func = () => account_app.ChangePassword("User99", "xyz", "NewPassw0rd");

        await func.Should().ThrowAsync<UserException>();
    }

    [Fact]
    public async Task ChangePassword_OK()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAccountApplication>();

        var result = await account_app.ChangePassword("User99", "abc", "NewPassw0rd");

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
    }

    [Fact]
    public async Task CreateTelegramLink_IssuesAOneTimeCodeAndRetiresThePendingOnes()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAccountApplication>();
        var account_repo_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
        var link_moq = scope.ServiceProvider.GetRequiredService<TelegramLinkRepositoryMoq>();

        var expired_account_ids = new List<int>();
        link_moq.OnExpireActiveCodes += expired_account_ids.Add;

        var result = await account_app.CreateTelegramLink("User99");

        Assert.Equal(2, result.Code / 100);
        result.Data.Should().NotBeNull();
        result.Data!.Code.Should().NotBeNullOrEmpty();
        result.Data.Code.Length.Should().BeLessThanOrEqualTo(64,
            "the Telegram start parameter is capped at 64 URL-safe characters");
        result.Data.Connected.Should().BeFalse();

        expired_account_ids.Should().BeEquivalentTo([account_repo_moq.Data["User99"].Id]);

        // only the newest code can bind: the previous one is consumed/dead
        var newest = await link_moq.Object.Consume(result.Data.Code!);
        newest.Should().NotBeNull();
        newest!.AccountId.Should().Be(account_repo_moq.Data["User99"].Id);
        newest.ExpireDate.Should().BeAfter(DateTime.Now);
    }

    [Fact]
    public async Task CreateTelegramLink_AlreadyConnected_ReturnsTheStateWithoutACode()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAccountApplication>();
        var account_repo_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
        var link_moq = scope.ServiceProvider.GetRequiredService<TelegramLinkRepositoryMoq>();

        account_repo_moq.Data["User99"].TelegramChatId = 555;

        var added_codes = 0;
        link_moq.OnAddCode += _ => added_codes++;

        var result = await account_app.CreateTelegramLink("User99");

        Assert.Equal(2, result.Code / 100);
        result.Data!.Connected.Should().BeTrue();
        result.Data.Code.Should().BeNull();
        added_codes.Should().Be(0);
    }

    [Fact]
    public async Task GetTelegramLinkStatus_ReflectsTheChatBinding()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAccountApplication>();
        var account_repo_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();

        (await account_app.GetTelegramLinkStatus("User99")).Data!.Connected.Should().BeFalse();

        account_repo_moq.Data["User99"].TelegramChatId = 555;

        (await account_app.GetTelegramLinkStatus("User99")).Data!.Connected.Should().BeTrue();
    }

    [Fact]
    public async Task DisconnectTelegram_ClearsTheChat()
    {
        using var scope = App.Services.CreateScope();
        var account_app = scope.ServiceProvider.GetRequiredService<IAccountApplication>();
        var account_repo_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();

        account_repo_moq.Data["User99"].TelegramChatId = 555;

        var result = await account_app.DisconnectTelegram("User99");

        Assert.Equal(2, result.Code / 100);
        account_repo_moq.Data["User99"].TelegramChatId.Should().BeNull();
    }
}