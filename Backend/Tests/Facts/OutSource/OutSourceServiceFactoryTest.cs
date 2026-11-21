using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PhotonBypass.OutSource;

namespace PhotonBypass.Test.Facts.OutSource;

/// <summary>
/// Conditional telegram registration (Docs/21 feature-flag): strict
/// TelegramOptions validation runs only while the channel is enabled.
/// </summary>
public class OutSourceServiceFactoryTest : IDisposable
{
    private readonly string temp_root = Path.Combine(Path.GetTempPath(), "outsource-factory-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void AddOutSourceServices_TelegramChannelWithoutBlock_FailsStartupValidation()
    {
        using var host = BuildChannelHost(new Dictionary<string, string?>
        {
            ["SocialMediaOptions:Channel"] = "Telegram",
        });

        var act = () => host.Start();

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddOutSourceServices_TelegramChannelWithIncompleteBlock_FailsStartupValidation()
    {
        var values = CompleteTelegramValues();
        values.Remove("TelegramOptions:Proxy:Address");

        using var host = BuildChannelHost(values);

        var act = () => host.Start();

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddOutSourceServices_TelegramChannelWithCompleteBlock_Starts()
    {
        using var host = BuildChannelHost(CompleteTelegramValues());

        var act = () => host.Start();

        act.Should().NotThrow();
    }

    [Fact]
    public void AddOutSourceServices_ChannelOffWithoutTelegramBlock_Starts()
    {
        // channel off: an absent/incomplete telegram block must not block
        // startup — nothing resolves the adapter (keyed DI None → Noop)
        using var host = BuildChannelHost(new Dictionary<string, string?>
        {
            ["SocialMediaOptions:Channel"] = "None",
        });

        var act = () => host.Start();

        act.Should().NotThrow();
    }

    /// <summary>
    /// Empty content root (no appsettings bleed-in from the test output
    /// directory) and a Production host: Development would validate every
    /// service descriptor at Build, but this slice alone cannot resolve them.
    /// ValidateOnStart fires on Start — that is the behavior under test.
    /// </summary>
    private IHost BuildChannelHost(Dictionary<string, string?> values)
    {
        values["BrandingOptions:Title"] = "Fast Bypass";
        values["BrandingOptions:SiteUrl"] = "https://photon-bypass.com";
        values["BrandingOptions:ConnectionGuideUrl"] = "https://guide.example.com/vpn";

        Directory.CreateDirectory(temp_root);

        var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Production,
            ContentRootPath = temp_root,
        });
        builder.Configuration.AddInMemoryCollection(values);

        builder.AddOutSourceServices();

        return builder.Build();
    }

    private static Dictionary<string, string?> CompleteTelegramValues() =>
        new()
        {
            ["SocialMediaOptions:Channel"] = "Telegram",
            ["TelegramOptions:BotToken"] = "1234567890:real-bot-token",
            ["TelegramOptions:BotUsername"] = "PhotonBypassBot",
            ["TelegramOptions:OperationsChatId"] = "1000000001",
            ["TelegramOptions:Proxy:Address"] = "http://127.0.0.1:9050",
        };

    public void Dispose()
    {
        if (Directory.Exists(temp_root))
        {
            Directory.Delete(temp_root, recursive: true);
        }
    }
}
