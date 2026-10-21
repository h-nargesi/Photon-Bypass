using PhotonBypass.Domain.Account.Entity;

namespace PhotonBypass.Domain.OutSource;

/// <summary>
/// Channel-neutral social notification contract: every method addresses the
/// account, never a raw address. Each adapter picks its own recipient field
/// (WhatsApp → <see cref="AccountEntity.MobileNumber"/>,
/// Telegram → <see cref="AccountEntity.TelegramChatId"/>), so application
/// services never learn the address type and swapping channels stays a
/// one-adapter change.
/// </summary>
public interface ISocialMediaService
{
    Task NewUserRegistrationAlert(AccountEntity account);

    Task SendResetPasswordLink(AccountEntity account, string hash_code);

    Task InvalidPasswordAlert(AccountEntity account);

    Task FinishServiceAlert(AccountEntity account, string plan_title, string remains_title);

    Task AlarmServerCapacity(IReadOnlyList<string> alarms);

    /// <summary>
    /// Whether this channel can reach the account (WhatsApp → a validated
    /// mobile, Telegram → a connected chat, Noop → never). Callers use it to
    /// pick between the social channel and the email fallback.
    /// </summary>
    bool Supports(AccountEntity account);
}
