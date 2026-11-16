using PhotonBypass.Domain.Account.Entity;

namespace PhotonBypass.Domain.OutSource;

/// <summary>
/// Channel-neutral social notification contract: every method addresses the
/// account, never a raw address. Each adapter picks its own recipient field
/// (WhatsApp → <see cref="PanelUserEntity.MobileNumber"/>,
/// Telegram → <see cref="PanelUserEntity.TelegramChatId"/>), so application
/// services never learn the address type and swapping channels stays a
/// one-adapter change.
/// </summary>
public interface ISocialMediaService
{
    Task NewUserRegistrationAlert(PanelUserEntity account);

    Task SendResetPasswordLink(PanelUserEntity account, string hash_code);

    Task InvalidPasswordAlert(PanelUserEntity account);

    Task FinishServiceAlert(PanelUserEntity account, string plan_title, string remains_title);

    Task AlarmServerCapacity(IReadOnlyList<string> alarms);

    /// <summary>
    /// Whether this channel can reach the account (WhatsApp → a validated
    /// mobile, Telegram → a connected chat, Noop → never). Callers use it to
    /// pick between the social channel and the email fallback.
    /// </summary>
    bool Supports(PanelUserEntity account);
}
