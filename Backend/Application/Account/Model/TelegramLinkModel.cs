namespace PhotonBypass.Application.Account.Model;

/// <summary>
/// Telegram connection state of one account: the status endpoints fill
/// <see cref="Connected"/> only, the link-code endpoint also the one-time
/// <see cref="Code"/> (and the caller — which knows the bot username —
/// completes <see cref="Url"/>).
/// </summary>
public class TelegramLinkModel
{
    public bool Connected { get; set; }

    public string? Code { get; set; }

    public string? Url { get; set; }
}
