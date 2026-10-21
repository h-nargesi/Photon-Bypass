using FluentAssertions;
using Microsoft.Extensions.Options;
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

    private TelegramMediaService CreateService()
    {
        return new TelegramMediaService(bot,
            Options.Create(new TelegramOptions { OperationsChatId = OperationsChatId }),
            Options.Create(new BrandingOptions
            {
                Title = "Fast Bypass",
                SiteUrl = "https://photon-bypass.com",
                ConnectionGuideUrl = "https://guide.example.com/vpn",
            }));
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
}
