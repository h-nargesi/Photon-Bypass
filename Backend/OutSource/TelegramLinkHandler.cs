using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.OutSource.Model;
using Serilog;

namespace PhotonBypass.OutSource;

/// <summary>
/// Scoped handler behind the poller: turns one bot update into account
/// linking (<c>/start CODE</c>). Separate from the poller loop so the whole
/// flow is unit-testable without a live bot or BackgroundService harness.
/// </summary>
internal sealed class TelegramLinkHandler(
    ITelegramLinkRepository link_repo,
    IAccountRepository account_repo,
    ITelegramBotClient bot_client,
    IOptions<BrandingOptions> branding_options)
{
    private const string StartCommand = "/start";

    public async Task Handle(TelegramUpdate update)
    {
        if (update.Message is not { Text: { } text } message) return;
        if (!TryParseStart(text, out var code)) return;

        if (code == null)
        {
            await bot_client.SendMessage(message.Chat.Id, HelpText());

            return;
        }

        var link = await link_repo.Consume(code);

        if (link == null)
        {
            await bot_client.SendMessage(message.Chat.Id,
                "کد اتصال نامعتبر است یا قبلاً استفاده شده است. از پنل کاربری کد جدید دریافت کنید.");

            return;
        }

        if (link.ExpireDate <= DateTime.Now)
        {
            await bot_client.SendMessage(message.Chat.Id,
                "کد اتصال منقضی شده است. از پنل کاربری کد جدید دریافت کنید.");

            return;
        }

        var account = await account_repo.GetActiveAccount(link.AccountId);

        if (account == null)
        {
            await bot_client.SendMessage(message.Chat.Id, "حساب کاربری یافت نشد یا غیرفعال است.");

            return;
        }

        try
        {
            await account_repo.SetTelegramChat(link.AccountId, message.Chat.Id);
        }
        catch (SqlException error) when (error.Number is 2601 or 2627)
        {
            // UK_Account_TelegramChatId: this chat is already bound elsewhere
            await bot_client.SendMessage(message.Chat.Id,
                "این گفتگو قبلاً به حساب دیگری متصل است. ابتدا اتصال را از آن حساب قطع کنید.");

            return;
        }

        Log.Information("Telegram chat {0} linked to account {1}", message.Chat.Id, account.Username);

        await bot_client.SendMessage(message.Chat.Id,
            $"حساب «{account.Username}» با موفقیت به تلگرام متصل شد. از این پس کدهای بازیابی و هشدارها به همین گفتگو ارسال می‌شود.");
    }

    private string HelpText()
    {
        var branding = branding_options.Value;

        return $"""
            سلام!
            برای اتصال حساب کاربری، از پنل کاربری کد اتصال دریافت کنید و سپس آن را به شکل «/start کد» در همین گفتگو ارسال نمایید.

            ورود به پنل کاربری: {branding.SiteUrl}

            تیم پشتیبانی | {branding.Title}
            """;
    }

    /// <summary>
    /// Accepts <c>/start</c>, <c>/start@Bot</c> and <c>/start CODE</c> (the
    /// deep-link form); everything else is ignored so the offset still moves
    /// on without answering random chat traffic.
    /// </summary>
    private static bool TryParseStart(string text, out string? code)
    {
        code = null;

        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 1) return false;

        var command = parts[0].Split('@')[0];

        if (!command.Equals(StartCommand, StringComparison.OrdinalIgnoreCase)) return false;

        if (parts.Length > 1) code = parts[1];

        return true;
    }
}
