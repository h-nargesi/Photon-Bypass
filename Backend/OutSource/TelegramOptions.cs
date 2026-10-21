using System.ComponentModel.DataAnnotations;

namespace PhotonBypass.OutSource;

/// <summary>
/// Telegram Bot API wiring (bound from the <c>TelegramOptions</c> section).
/// Committed values are test fixtures — production overrides them per key
/// from <c>private/secrets.json</c> (ProductionSecretsGuard enforces it).
/// </summary>
public class TelegramOptions
{
    [Required]
    public string BotToken { get; set; } = null!;

    /// <summary>Bot username without '@' — the portal deep link is built from it.</summary>
    [Required]
    public string BotUsername { get; set; } = null!;

    [Required]
    [Url]
    public string ApiBase { get; set; } = "https://api.telegram.org";

    /// <summary>Chat id receiving the operator alerts (registration + capacity).</summary>
    [Range(1, long.MaxValue)]
    public long OperationsChatId { get; set; }

    /// <summary>Outbound proxy — in Iran the API is unreachable without one.</summary>
    public TelegramProxyOptions? Proxy { get; set; }
}

public class TelegramProxyOptions
{
    [Required]
    public string Address { get; set; } = null!;

    public string? Username { get; set; }

    public string? Password { get; set; }
}
