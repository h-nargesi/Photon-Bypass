using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Tools;
using System.Net;

namespace PhotonBypass.OutSource;

public static class ServiceFactory
{
    /// <summary>Named HttpClient shared by sendMessage and getUpdates (proxy-routed).</summary>
    public const string HttpClientName = "telegram";

    public static void AddOutSourceServices<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.BindValidateReturn<BrandingOptions>();
        builder.Services.BindValidateReturn<SocialMediaOptions>();

        // feature-flag pattern (Docs/21): strict telegram validation applies
        // only when the channel is actually enabled — with the channel off
        // nothing resolves the adapter (keyed DI None → Noop, poller idles,
        // telegram HttpClient is lazy), so an absent block must not block
        // startup. Same registration-time string read the channel dispatcher
        // binds from.
        var telegram_channel_enabled =
            builder.Configuration["SocialMediaOptions:Channel"] == nameof(SocialChannel.Telegram);

        if (telegram_channel_enabled)
        {
            builder.Services.BindValidateReturn<TelegramOptions>();

            builder.Services.AddOptions<TelegramOptions>()
                .Validate(options => !string.IsNullOrWhiteSpace(options.Proxy?.Address),
                    "TelegramOptions:Proxy:Address must be set — the Bot API is unreachable from the server without an outbound proxy.")
                .ValidateOnStart();
        }
        else
        {
            builder.Services.AddOptions<TelegramOptions>()
                .BindConfiguration(nameof(TelegramOptions));
        }

        builder.Services.AddHttpClient(HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(provider =>
            {
                var proxy_options = provider.GetRequiredService<IOptions<TelegramOptions>>().Value.Proxy;
                var handler = new HttpClientHandler();

                if (proxy_options != null)
                {
                    var proxy = new WebProxy(proxy_options.Address);

                    if (proxy_options.Username != null)
                    {
                        proxy.Credentials = new NetworkCredential(proxy_options.Username,
                            proxy_options.Password ?? string.Empty);
                    }

                    handler.Proxy = proxy;
                }

                return handler;
            });

        builder.Services.AddLazyScoped<IEmailService, EmailService>();
        builder.Services.AddLazyScoped<ITelegramBotClient, TelegramBotClient>();
        builder.Services.AddScoped<TelegramLinkHandler>();

        // one keyed adapter per channel (Radius-style dispatch); WhatsApp is
        // the reserved seam for its future adapter
        builder.Services.AddLazyKeyedTransient<ISocialMediaService, TelegramMediaService>(SocialChannel.Telegram);
        builder.Services.AddLazyKeyedTransient<ISocialMediaService, NoopSocialMediaService>(SocialChannel.None);

        // runtime channel selection: the configured channel's adapter is the
        // plain ISocialMediaService every application service consumes
        // (GetRequiredKeyedService fails fast on an unregistered channel,
        // e.g. a future WhatsApp value before its adapter exists)
        builder.Services.AddLazyTransient<ISocialMediaService>(provider =>
            provider.GetRequiredKeyedService<ISocialMediaService>(
                provider.GetRequiredService<IOptions<SocialMediaOptions>>().Value.Channel));
    }
}
