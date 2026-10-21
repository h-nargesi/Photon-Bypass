using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace PhotonBypass.OutSource;

/// <summary>
/// Failure reported by the Bot API (ok=false): 429 rate limit, 409 conflict,
/// 400 bad request, … — surfaced to callers that decide between retry and log.
/// </summary>
public sealed class TelegramBotException(int error_code, string description, Exception? inner = null)
    : Exception($"Telegram Bot API call failed ({error_code}): {description}", inner)
{
    public int ErrorCode { get; } = error_code;

    public string Description { get; } = description;
}

internal sealed class TelegramBotClient(IHttpClientFactory http_factory, IOptions<TelegramOptions> options) : ITelegramBotClient
{
    private static readonly TimeSpan RateLimitGrace = TimeSpan.FromSeconds(1);

    public async Task SendMessage(long chat_id, string text, CancellationToken cancellation = default)
    {
        using var client = http_factory.CreateClient(ServiceFactory.HttpClientName);

        // one Retry-After-honoring retry: senders are best-effort paths and a
        // second 429 must surface instead of looping
        for (var attempt = 0; ; attempt++)
        {
            using var response = await client.PostAsJsonAsync(MethodUrl("sendMessage"),
                new SendMessageRequest(chat_id, text), cancellation);

            var body = await response.Content.ReadFromJsonAsync<ApiResponse>(cancellation);

            if (body is { Ok: true })
            {
                return;
            }

            if (body?.ErrorCode == 429 && attempt == 0 && body.Parameters?.RetryAfter is int retry_after)
            {
                await Task.Delay(TimeSpan.FromSeconds(retry_after) + RateLimitGrace, cancellation);

                continue;
            }

            throw new TelegramBotException(
                body?.ErrorCode ?? (int)response.StatusCode,
                body?.Description ?? $"HTTP {(int)response.StatusCode}");
        }
    }

    public async Task<List<TelegramUpdate>> GetUpdates(long offset, int timeout_seconds, CancellationToken cancellation = default)
    {
        using var client = http_factory.CreateClient(ServiceFactory.HttpClientName);

        using var response = await client.GetAsync(
            $"{MethodUrl("getUpdates")}?offset={offset}&timeout={timeout_seconds}", cancellation);

        var body = await response.Content.ReadFromJsonAsync<GetUpdatesResponse>(cancellation);

        if (body is not { Ok: true })
        {
            throw new TelegramBotException(body?.ErrorCode ?? (int)response.StatusCode,
                body?.Description ?? $"HTTP {(int)response.StatusCode}");
        }

        return body.Result ?? [];
    }

    private string MethodUrl(string method)
    {
        return $"{options.Value.ApiBase.TrimEnd('/')}/bot{options.Value.BotToken}/{method}";
    }

    private sealed record SendMessageRequest(
        [property: JsonPropertyName("chat_id")] long ChatId,
        [property: JsonPropertyName("text")] string Text);

    private sealed record ApiResponse(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("error_code")] int? ErrorCode,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("parameters")] ResponseParameters? Parameters);

    private sealed record ResponseParameters(
        [property: JsonPropertyName("retry_after")] int? RetryAfter);

    private sealed record GetUpdatesResponse(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("error_code")] int? ErrorCode,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("parameters")] ResponseParameters? Parameters,
        [property: JsonPropertyName("result")] List<TelegramUpdate>? Result);
}
