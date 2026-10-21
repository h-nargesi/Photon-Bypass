using System.Text.Json.Serialization;

namespace PhotonBypass.OutSource;

public interface ITelegramBotClient
{
    /// <summary>
    /// Sends one plain-text message. A 429 is honored (Retry-After header of
    /// the Bot API) with a single retry; every other error throws.
    /// </summary>
    Task SendMessage(long chat_id, string text, CancellationToken cancellation = default);

    /// <summary>
    /// Long-poll fetch; the caller owns the offset (last update_id + 1).
    /// </summary>
    Task<List<TelegramUpdate>> GetUpdates(long offset, int timeout_seconds, CancellationToken cancellation = default);
}

public sealed record TelegramUpdate(
    [property: JsonPropertyName("update_id")] long UpdateId,
    [property: JsonPropertyName("message")] TelegramMessage? Message);

public sealed record TelegramMessage(
    [property: JsonPropertyName("message_id")] long MessageId,
    [property: JsonPropertyName("chat")] TelegramChat Chat,
    [property: JsonPropertyName("text")] string? Text);

public sealed record TelegramChat([property: JsonPropertyName("id")] long Id);
