using PhotonBypass.OutSource;

namespace PhotonBypass.Test.Mock.MockOutSource;

/// <summary>
/// Sender-only stand-in for <see cref="ITelegramBotClient"/>: records every
/// message and lets a test fail specific sends through <see cref="OnSendMessage"/>.
/// </summary>
internal sealed class TelegramBotClientFake : ITelegramBotClient
{
    public List<(long ChatId, string Text)> Sent { get; } = [];

    public Func<long, string, Task>? OnSendMessage { get; set; }

    public Task SendMessage(long chat_id, string text, CancellationToken cancellation = default)
    {
        Sent.Add((chat_id, text));

        return OnSendMessage?.Invoke(chat_id, text) ?? Task.CompletedTask;
    }

    public Task<List<TelegramUpdate>> GetUpdates(long offset, int timeout_seconds, CancellationToken cancellation = default)
    {
        throw new NotSupportedException("The fake is a sender-only stand-in; the poller has its own tests.");
    }
}
