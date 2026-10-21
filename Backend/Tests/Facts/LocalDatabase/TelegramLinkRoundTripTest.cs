using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.OutSource;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;
using PhotonBypass.Test.Mock.MockOutSource;

namespace PhotonBypass.Test.Facts.LocalDatabase;

/// <summary>
/// End-to-end link flow on the scenario database: the real repositories, the
/// real filtered unique index and the real TelegramLink table, with only the
/// bot transport faked.
/// </summary>
public class TelegramLinkRoundTripTest : OutSourceLevelServiceInitializer
{
    private const string TestPackage = "TelegramLink";

    private readonly TelegramBotClientFake bot = new();

    protected override void AddClassTestServices(IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<ITelegramBotClient>(_ => bot);
    }

    [Fact]
    public async Task LinkCodeLifecycle_ConnectsReplaysOnceAndExpires()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var account_repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var link_repo = scope.ServiceProvider.GetRequiredService<ITelegramLinkRepository>();
        var handler = scope.ServiceProvider.GetRequiredService<TelegramLinkHandler>();

        var account = await account_repo.GetAccount("tg-user");

        account.Should().NotBeNull();

        // self-contained start: detach whatever a sibling test left behind
        await account_repo.SetTelegramChat(account!.Id, null);

        // issuing a new code retires the account's pending codes
        await link_repo.AddCode(new TelegramLinkEntity
        {
            AccountId = account.Id,
            Code = "stale-code",
            ExpireDate = DateTime.Now.AddMinutes(15),
        });
        await link_repo.ExpireActiveCodes(account.Id);

        (await link_repo.Consume("stale-code"))!.ExpireDate.Should().BeOnOrBefore(DateTime.Now);

        await link_repo.AddCode(new TelegramLinkEntity
        {
            AccountId = account.Id,
            Code = "live-code",
            ExpireDate = DateTime.Now.AddMinutes(15),
        });

        // /start with the live code binds the chat
        await handler.Handle(Update(555, "/start live-code"));

        (await account_repo.GetAccount("tg-user"))!.TelegramChatId.Should().Be(555);

        bot.Sent.Should().Contain(message => message.ChatId == 555 && message.Text.Contains("متصل شد"));

        // replaying the consumed code is inert
        await handler.Handle(Update(555, "/start live-code"));

        bot.Sent.Should().Contain(message => message.Text.Contains("نامعتبر"));
        (await link_repo.Consume("live-code")).Should().BeNull();

        // detaching clears the binding
        await account_repo.SetTelegramChat(account.Id, null);

        (await account_repo.GetAccount("tg-user"))!.TelegramChatId.Should().BeNull();
    }

    [Fact]
    public async Task ChatAlreadyBoundToAnotherAccount_IsRejectedByTheUniqueIndex()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var account_repo = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var link_repo = scope.ServiceProvider.GetRequiredService<ITelegramLinkRepository>();
        var handler = scope.ServiceProvider.GetRequiredService<TelegramLinkHandler>();

        var first = await account_repo.GetAccount("tg-user");
        var second = await account_repo.GetAccount("tg-user-2");

        await account_repo.SetTelegramChat(first!.Id, 777);

        await link_repo.AddCode(new TelegramLinkEntity
        {
            AccountId = second!.Id,
            Code = "second-code",
            ExpireDate = DateTime.Now.AddMinutes(15),
        });

        // the same chat tries to link the second account
        await handler.Handle(Update(777, "/start second-code"));

        (await account_repo.GetAccount("tg-user-2"))!.TelegramChatId.Should().BeNull();
        bot.Sent.Should().Contain(message => message.Text.Contains("حساب دیگری"));

        // and the plain repository path surfaces the raw unique-index error
        var action = () => account_repo.SetTelegramChat(second.Id, 777);

        (await action.Should().ThrowAsync<SqlException>())
            .Which.Number.Should().BeOneOf(2601, 2627);
    }

    private static TelegramUpdate Update(long chat_id, string text)
    {
        return new TelegramUpdate(1, new TelegramMessage(1, new TelegramChat(chat_id), text));
    }
}
