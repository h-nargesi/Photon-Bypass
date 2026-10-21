using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.Domain.OutSource.Model;
using Serilog;

namespace PhotonBypass.OutSource;

/// <summary>
/// Telegram long-poll loop (<c>getUpdates</c>, 25s server-side wait) — the
/// IIS/Plesk-friendly single-instance alternative to a webhook: the ~25s
/// cycle keeps the worker alive exactly like the Quartz JobInterval does.
/// Registered only in <c>AddPortalServices</c>: a second concurrent poller
/// (e.g. a future Admin host deployment) would make Telegram answer 409 to
/// one of the two loops.
/// </summary>
public sealed class TelegramUpdatePoller(
    IServiceScopeFactory scope_factory,
    IOptions<SocialMediaOptions> social_options) : BackgroundService
{
    private const int PollTimeoutSeconds = 25;

    private static readonly TimeSpan MinBackoff = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stopping_token)
    {
        if (social_options.Value.Channel != SocialChannel.Telegram)
        {
            Log.Debug("TelegramUpdatePoller idle: SocialMediaOptions:Channel is not Telegram.");

            return;
        }

        // in-memory offset: after a recycle the last update is redelivered —
        // harmless, link codes are one-time and SetTelegramChat is idempotent
        var offset = 0L;
        var backoff = MinBackoff;

        while (!stopping_token.IsCancellationRequested)
        {
            List<TelegramUpdate> updates;

            try
            {
                using var scope = scope_factory.CreateScope();

                updates = await scope.ServiceProvider.GetRequiredService<ITelegramBotClient>()
                    .GetUpdates(offset, PollTimeoutSeconds, stopping_token);
            }
            catch (OperationCanceledException) when (stopping_token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                Log.Error(error,
                    "Telegram getUpdates failed; backing off {0}s (409 would mean another poller is running).",
                    backoff.TotalSeconds);

                if (!await DelayAsync(backoff, stopping_token)) break;

                backoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, MaxBackoff.Ticks));

                continue;
            }

            // the poller is a singleton while the handler's repositories are
            // scoped: one scope per batch keeps the captive-dependency rule
            using (var scope = scope_factory.CreateScope())
            {
                var handler = scope.ServiceProvider.GetRequiredService<TelegramLinkHandler>();

                foreach (var update in updates)
                {
                    // advance before handling: a poison update must not pin
                    // the loop on the same offset forever
                    offset = update.UpdateId + 1;

                    try
                    {
                        await handler.Handle(update);
                    }
                    catch (OperationCanceledException) when (stopping_token.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception error)
                    {
                        Log.Error(error, "Telegram update handling failed: update-id={0}", update.UpdateId);
                    }
                }
            }

            backoff = MinBackoff;
        }
    }

    private static async Task<bool> DelayAsync(TimeSpan delay, CancellationToken stopping_token)
    {
        try
        {
            await Task.Delay(delay, stopping_token);
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        return true;
    }
}
