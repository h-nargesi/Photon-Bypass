using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Moq;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.OutSource;
using PhotonBypass.Test.Mock.MockLocalRepository;
using PhotonBypass.Test.Mock.MockOutSource;
using System.Reflection;

namespace PhotonBypass.Test.Facts.OutSource;

public class TelegramLinkHandlerTest
{
    private static readonly IOptions<BrandingOptions> Branding = Options.Create(new BrandingOptions
    {
        Title = "Fast Bypass",
        SiteUrl = "https://photon-bypass.com",
    });

    private readonly TelegramLinkRepositoryMoq link_repo_moq = new();

    private readonly Mock<IAccountRepository> account_repo_moq = new();

    private readonly TelegramBotClientFake bot = new();

    private TelegramLinkHandler Handler => new(link_repo_moq.Object, account_repo_moq.Object, bot, Branding);

    private static TelegramUpdate StartUpdate(long chat_id, string? code)
    {
        return new TelegramUpdate(101, new TelegramMessage(1, new TelegramChat(chat_id),
            code == null ? "/start" : $"/start {code}"));
    }

    private static AccountEntity ActiveAccount(int id, string username)
    {
        return new AccountEntity { Id = id, Username = username, Password = "hash" };
    }

    [Fact]
    public async Task ValidCode_ConsumesAndConnectsTheChat()
    {
        link_repo_moq.Object.AddCode(new TelegramLinkEntity
        {
            AccountId = 5,
            Code = "the-code",
            ExpireDate = DateTime.Now.AddMinutes(10),
        });

        var account = ActiveAccount(5, "tg-user");

        account_repo_moq.Setup(repo => repo.GetActiveAccount(5)).ReturnsAsync(account);

        int? bound_account = null;
        long? bound_chat = null;

        account_repo_moq.Setup(repo => repo.SetTelegramChat(It.IsAny<int>(), It.IsAny<long?>()))
            .Callback<int, long?>((account_id, chat_id) => (bound_account, bound_chat) = (account_id, chat_id))
            .Returns(Task.CompletedTask);

        await Handler.Handle(StartUpdate(555, "the-code"));

        bound_account.Should().Be(5);
        bound_chat.Should().Be(555);

        bot.Sent.Should().ContainSingle().Which.ChatId.Should().Be(555);
        bot.Sent[0].Text.Should().Contain("tg-user").And.Contain("متصل شد");
    }

    [Fact]
    public async Task ExpiredCode_IsRejectedWithoutBinding()
    {
        link_repo_moq.Object.AddCode(new TelegramLinkEntity
        {
            AccountId = 5,
            Code = "late-code",
            ExpireDate = DateTime.Now.AddMinutes(-1),
        });

        account_repo_moq.Setup(repo => repo.GetActiveAccount(It.IsAny<int>()))
            .ReturnsAsync(ActiveAccount(5, "tg-user"));

        await Handler.Handle(StartUpdate(555, "late-code"));

        account_repo_moq.Verify(repo => repo.SetTelegramChat(It.IsAny<int>(), It.IsAny<long?>()), Times.Never);
        bot.Sent.Should().ContainSingle().Which.Text.Should().Contain("منقضی");
    }

    [Fact]
    public async Task ReplayedCode_IsAlreadyConsumed()
    {
        link_repo_moq.Object.AddCode(new TelegramLinkEntity
        {
            AccountId = 5,
            Code = "once-code",
            ExpireDate = DateTime.Now.AddMinutes(10),
        });

        account_repo_moq.Setup(repo => repo.GetActiveAccount(It.IsAny<int>()))
            .ReturnsAsync(ActiveAccount(5, "tg-user"));
        account_repo_moq.Setup(repo => repo.SetTelegramChat(It.IsAny<int>(), It.IsAny<long?>()))
            .Returns(Task.CompletedTask);

        await Handler.Handle(StartUpdate(555, "once-code"));
        await Handler.Handle(StartUpdate(555, "once-code"));

        // one bind + one "invalid" reply for the replay; never a second bind
        account_repo_moq.Verify(repo => repo.SetTelegramChat(It.IsAny<int>(), It.IsAny<long?>()), Times.Once);
        bot.Sent.Should().HaveCount(2);
        bot.Sent[1].Text.Should().Contain("نامعتبر");
    }

    [Fact]
    public async Task UnknownCode_GetsTheInvalidReply()
    {
        await Handler.Handle(StartUpdate(555, "ghost-code"));

        account_repo_moq.Verify(repo => repo.SetTelegramChat(It.IsAny<int>(), It.IsAny<long?>()), Times.Never);
        bot.Sent.Should().ContainSingle().Which.Text.Should().Contain("نامعتبر");
    }

    [Fact]
    public async Task DeactivatedAccount_IsNotBound()
    {
        link_repo_moq.Object.AddCode(new TelegramLinkEntity
        {
            AccountId = 5,
            Code = "dead-account-code",
            ExpireDate = DateTime.Now.AddMinutes(10),
        });

        account_repo_moq.Setup(repo => repo.GetActiveAccount(5)).ReturnsAsync((AccountEntity?)null);

        await Handler.Handle(StartUpdate(555, "dead-account-code"));

        account_repo_moq.Verify(repo => repo.SetTelegramChat(It.IsAny<int>(), It.IsAny<long?>()), Times.Never);
        bot.Sent.Should().ContainSingle().Which.Text.Should().Contain("غیرفعال");
    }

    [Fact]
    public async Task DuplicateChat_ExplainsTheConflict()
    {
        link_repo_moq.Object.AddCode(new TelegramLinkEntity
        {
            AccountId = 5,
            Code = "dup-code",
            ExpireDate = DateTime.Now.AddMinutes(10),
        });

        account_repo_moq.Setup(repo => repo.GetActiveAccount(It.IsAny<int>()))
            .ReturnsAsync(ActiveAccount(5, "tg-user"));

        account_repo_moq.Setup(repo => repo.SetTelegramChat(It.IsAny<int>(), It.IsAny<long?>()))
            .ThrowsAsync(CreateDuplicateKeyException());

        await Handler.Handle(StartUpdate(555, "dup-code"));

        bot.Sent.Should().ContainSingle().Which.Text.Should().Contain("حساب دیگری");
    }

    [Fact]
    public async Task StartWithoutCode_RepliesWithThePortalHelp()
    {
        await Handler.Handle(StartUpdate(555, null));

        bot.Sent.Should().ContainSingle();
        bot.Sent[0].Text.Should().Contain("https://photon-bypass.com");
        bot.Sent[0].Text.Should().Contain("/start");
    }

    [Theory]
    [InlineData("hello there")]
    [InlineData("/help")]
    [InlineData("/startx the-code")]
    public async Task NonStartTraffic_IsIgnoredSilently(string text)
    {
        var update = new TelegramUpdate(101, new TelegramMessage(1, new TelegramChat(555), text));

        await Handler.Handle(update);

        bot.Sent.Should().BeEmpty();
    }

    /// <summary>
    /// Builds the real <see cref="SqlException"/> the filtered unique index
    /// raises (2601) — the handler's catch filter only matches the genuine
    /// type, so a substitute exception would not exercise the branch. The
    /// internal constructor shapes move between SqlClient versions, so the
    /// arguments are mapped by parameter name with type-default fallbacks.
    /// </summary>
    private static SqlException CreateDuplicateKeyException()
    {
        var values = new Dictionary<string, object>
        {
            ["infoNumber"] = 2601,
            ["errorState"] = 1,
            ["errorClass"] = 14,
            ["server"] = "server",
            ["errorMessage"] = "Cannot insert duplicate key row",
            ["procedure"] = "proc",
            ["lineNumber"] = 1,
            ["win32ErrorCode"] = 0,
            ["exception"] = new Exception("duplicate key"),
        };

        var error_ctor = typeof(SqlError).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Select(ctor => (ctor, matches: ctor.GetParameters().Count(p => values.ContainsKey(p.Name!))))
            .OrderByDescending(entry => entry.matches)
            .Select(entry => entry.ctor)
            .First();

        var error = error_ctor.Invoke([.. error_ctor.GetParameters()
            .Select(parameter => MapValue(parameter, values))]);

        var collection = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), nonPublic: true)!;

        typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(collection, [error]);

        var create_exception = typeof(SqlException).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .First(method => method.Name == "CreateException"
                             && method.GetParameters().Length == 2
                             && method.GetParameters()[0].ParameterType == typeof(SqlErrorCollection));

        return (SqlException)create_exception.Invoke(null,
        [
            collection,
            create_exception.GetParameters()[1].ParameterType == typeof(string) ? "7.0" : null,
        ])!;
    }

    private static object? MapValue(ParameterInfo parameter, Dictionary<string, object> values)
    {
        if (!values.TryGetValue(parameter.Name!, out var value))
        {
            return parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null;
        }

        if (parameter.ParameterType.IsInstanceOfType(value))
        {
            return value;
        }

        if (value is IConvertible && parameter.ParameterType.IsPrimitive)
        {
            return Convert.ChangeType(value, parameter.ParameterType);
        }

        return parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null;
    }
}
