using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.OutSource;
using PhotonBypass.Test.Mock.MockOutSource;

namespace PhotonBypass.Test.Facts.OutSource;

public class TelegramMediaServiceTest
{
    private const long UserChatId = 555;

    private const long OperationsChatId = 1000000001;

    private readonly TelegramBotClientFake bot = new();

    private readonly Mock<IAccountRepository> account_repo = new();

    private TelegramMediaService CreateService()
    {
        return new TelegramMediaService(bot,
            Options.Create(new TelegramOptions { OperationsChatId = OperationsChatId }),
            Options.Create(new BrandingOptions
            {
                Title = "Fast Bypass",
                SiteUrl = "https://photon-bypass.com",
                ConnectionGuideUrl = "https://guide.example.com/vpn",
            }),
            new Lazy<IAccountRepository>(() => account_repo.Object));
    }

    private static AccountEntity Account(long? chat_id)
    {
        return new AccountEntity
        {
            Id = 5,
            Username = "tg-user",
            Name = "Test",
            Surname = "User",
            Password = "hash",
            TelegramChatId = chat_id,
        };
    }

    [Fact]
    public void Supports_RequiresAConnectedChat()
    {
        CreateService().Supports(Account(null)).Should().BeFalse();
        CreateService().Supports(Account(UserChatId)).Should().BeTrue();
    }

    [Fact]
    public async Task SendResetPasswordLink_GoesToTheAccountChat()
    {
        await CreateService().SendResetPasswordLink(Account(UserChatId), "the-reset-code");

        bot.Sent.Should().ContainSingle();
        bot.Sent[0].ChatId.Should().Be(UserChatId);
        bot.Sent[0].Text.Should().Contain("the-reset-code").And.Contain("tg-user");
    }

    [Fact]
    public async Task UserAlertsWithoutAChat_AreSilentNoOps()
    {
        var service = CreateService();

        await service.SendResetPasswordLink(Account(null), "code");
        await service.InvalidPasswordAlert(Account(null));
        await service.FinishServiceAlert(Account(null), "plan", "3 روز");

        bot.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task InvalidPasswordAlert_GoesToTheAccountChat()
    {
        await CreateService().InvalidPasswordAlert(Account(UserChatId));

        bot.Sent.Should().ContainSingle().Which.ChatId.Should().Be(UserChatId);
        bot.Sent[0].Text.Should().Contain("tg-user");
    }

    [Fact]
    public async Task FinishServiceAlert_CarriesThePlanDetails()
    {
        await CreateService().FinishServiceAlert(Account(UserChatId), "50GB-Plan", "3 روز");

        bot.Sent.Should().ContainSingle().Which.ChatId.Should().Be(UserChatId);
        bot.Sent[0].Text.Should().Contain("50GB-Plan").And.Contain("3 روز");
    }

    [Fact]
    public async Task NewUserRegistrationAlert_GoesToTheOperationsChat()
    {
        await CreateService().NewUserRegistrationAlert(Account(UserChatId));

        bot.Sent.Should().ContainSingle().Which.ChatId.Should().Be(OperationsChatId);
        bot.Sent[0].Text.Should().Contain("tg-user");
    }

    [Fact]
    public async Task AlarmServerCapacity_GoesToTheOperationsChat()
    {
        await CreateService().AlarmServerCapacity(["Low Capacity: Realm-1", "Low Capacity (clients): Realm-3"]);

        bot.Sent.Should().ContainSingle().Which.ChatId.Should().Be(OperationsChatId);
        bot.Sent[0].Text.Should().Contain("Realm-1").And.Contain("Realm-3");
    }

    [Fact]
    public async Task BlockedBotChat_DetachesTheAccountAndRethrows()
    {
        bot.OnSendMessage = (_, _) => throw new TelegramBotException(403, "Forbidden: bot was blocked by the user");

        var action = () => CreateService().SendResetPasswordLink(Account(UserChatId), "code");

        await action.Should().ThrowAsync<TelegramBotException>();

        account_repo.Verify(repo => repo.SetTelegramChat(5, null), Times.Once);
    }

    [Fact]
    public async Task ChatNotFound_DetachesTheAccountAndRethrows()
    {
        bot.OnSendMessage = (_, _) => throw new TelegramBotException(400, "Bad Request: chat not found");

        var action = () => CreateService().InvalidPasswordAlert(Account(UserChatId));

        await action.Should().ThrowAsync<TelegramBotException>();

        account_repo.Verify(repo => repo.SetTelegramChat(5, null), Times.Once);
    }

    [Fact]
    public async Task OtherBadRequestErrors_DoNotDetachTheChat()
    {
        bot.OnSendMessage = (_, _) => throw new TelegramBotException(400, "Bad Request: message is too long");

        var action = () => CreateService().FinishServiceAlert(Account(UserChatId), "50GB-Plan", "3 روز");

        await action.Should().ThrowAsync<TelegramBotException>();

        account_repo.Verify(repo => repo.SetTelegramChat(It.IsAny<int>(), It.IsAny<long?>()), Times.Never);
    }

    [Fact]
    public async Task RateLimitExhausted_DoesNotDetachTheChat()
    {
        bot.OnSendMessage = (_, _) => throw new TelegramBotException(429, "Too Many Requests: retry after 5");

        var action = () => CreateService().SendResetPasswordLink(Account(UserChatId), "code");

        await action.Should().ThrowAsync<TelegramBotException>();

        account_repo.Verify(repo => repo.SetTelegramChat(It.IsAny<int>(), It.IsAny<long?>()), Times.Never);
    }

    [Fact]
    public async Task OperationsChatFailure_DetachesNoAccount()
    {
        bot.OnSendMessage = (_, _) => throw new TelegramBotException(403, "Forbidden: bot was blocked by the user");

        var action = () => CreateService().NewUserRegistrationAlert(Account(UserChatId));

        await action.Should().ThrowAsync<TelegramBotException>();

        // OperationsChatId is config, not an account record: nothing to detach
        account_repo.Verify(repo => repo.SetTelegramChat(It.IsAny<int>(), It.IsAny<long?>()), Times.Never);
    }

    [Fact]
    public async Task DetachFailure_StillRethrowsTheOriginalSendError()
    {
        bot.OnSendMessage = (_, _) => throw new TelegramBotException(403, "Forbidden: bot was blocked by the user");

        account_repo.Setup(repo => repo.SetTelegramChat(5, null))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var action = () => CreateService().SendResetPasswordLink(Account(UserChatId), "code");

        // the best-effort detach must not mask the failure the caller's
        // email fallback depends on
        (await action.Should().ThrowAsync<TelegramBotException>())
            .Which.ErrorCode.Should().Be(403);
    }
}
