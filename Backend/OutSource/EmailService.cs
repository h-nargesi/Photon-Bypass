using Microsoft.Extensions.Options;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.ServerBridge.Services;
using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;

namespace PhotonBypass.OutSource;

class EmailService(IEmailHandler handler, IOptions<BrandingOptions> branding_options) : IEmailService
{
    private const string TemplateFolderName = "EmailTemplates";

    private static readonly ConcurrentDictionary<string, Task<string>> TemplateCache = new();
    private static readonly ConcurrentDictionary<string, Task<byte[]>> LogoCache = new();

    private static async Task<string> LoadTemplateAsync(string path)
    {
        var task = TemplateCache.GetOrAdd(path, p => File.ReadAllTextAsync(p));

        try
        {
            return await task;
        }
        catch
        {
            // a faulted task must not stay cached: a single transient read
            // failure would break every subsequent send until a restart
            TemplateCache.TryRemove(path, out _);
            throw;
        }
    }

    /// <summary>
    /// Loads the branding logo once per path; a missing file fails fast with a
    /// clear message because every outbound email would otherwise go out broken.
    /// </summary>
    private static async Task<byte[]> LoadLogoAsync(string path)
    {
        var task = LogoCache.GetOrAdd(path, async p =>
        {
            if (!File.Exists(p))
            {
                throw new FileNotFoundException($"Branding logo file not found: {p}", p);
            }

            return await File.ReadAllBytesAsync(p);
        });

        try
        {
            return await task;
        }
        catch
        {
            LogoCache.TryRemove(path, out _);
            throw;
        }
    }

    /// <summary>
    /// Root for template and relative-logo paths: the application base
    /// directory, where the build/publish pipeline places EmailTemplates and
    /// Branding — stable regardless of the process working directory or the
    /// host's content root (fix for the CWD-dependent lookups).
    /// </summary>
    private static string AppBasePath => AppContext.BaseDirectory;

    private string TemplatePath(string template_name, string extension)
    {
        return Path.Combine(AppBasePath, TemplateFolderName, $"{template_name}.{extension}");
    }

    private string LogoPath()
    {
        var branding = branding_options.Value;

        return Path.IsPathRooted(branding.LogoPath)
            ? branding.LogoPath
            : Path.Combine(AppBasePath, branding.LogoPath);
    }

    /// <summary>
    /// Loads the HTML and plain-text views of one template; both share the
    /// same placeholders and conditional markers (the markers never survive
    /// rendering: <see cref="RenderSection"/> strips them from both views).
    /// </summary>
    private async Task<(string Html, string Text)> LoadTemplatePairAsync(string template_name)
    {
        var html = await LoadTemplateAsync(TemplatePath(template_name, "html"));
        var text = await LoadTemplateAsync(TemplatePath(template_name, "txt"));

        return (html, text);
    }

    private async Task<EmailMessage> CreateMessageAsync(string to_name, string to_address, string subject,
        (string Html, string Text) bodies, IReadOnlyList<EmailAttachment>? attachments = null)
    {
        var branding = branding_options.Value;
        var logo = await LoadLogoAsync(LogoPath());

        return new EmailMessage
        {
            ToName = to_name,
            ToAddress = to_address,
            Subject = subject,
            HtmlBody = bodies.Html,
            PlainTextBody = bodies.Text,
            EmbeddedLogo = logo,
            LogoContentType = LogoContentType(branding.LogoPath),
            Attachments = attachments ?? [],
        };
    }

    private static string LogoContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            _ => "image/png",
        };
    }

    /// <summary>
    /// Applies the shared brand values (footer link/title, guide links) before
    /// the per-email placeholders; templates without the markers are unaffected.
    /// </summary>
    private string ApplyBranding(string body)
    {
        var branding = branding_options.Value;

        // the OpenVPN installer line only exists when its URL is configured;
        // for templates without the marker this is a no-op
        body = RenderSection(body, "installer", branding.OvpnInstallerUrl);

        return body.Replace("{title}", branding.Title)
            .Replace("{site_url}", branding.SiteUrl)
            .Replace("{guide_url}", branding.ConnectionGuideUrl)
            .Replace("{ovpn_installer_url}", branding.OvpnInstallerUrl ?? string.Empty);
    }

    /// <summary>
    /// Applies branding plus the per-placeholder values to both views. User
    /// input is HTML-encoded in the HTML view only � never in plain text and
    /// never for server-generated codes/links that must stay verbatim.
    /// </summary>
    private (string Html, string Text) RenderPair((string Html, string Text) bodies,
        params (string Placeholder, string Value, bool EncodeInHtml)[] values)
    {
        var (html, text) = (ApplyBranding(bodies.Html), ApplyBranding(bodies.Text));

        foreach (var (placeholder, value, encode) in values)
        {
            html = html.Replace(placeholder, encode ? WebUtility.HtmlEncode(value) : value);
            text = text.Replace(placeholder, value);
        }

        return (html, text);
    }

    public async Task FinishServiceAlert(string fullname, string username, string email, string type, string left)
    {
        if (string.IsNullOrWhiteSpace(fullname))
        {
            fullname = username;
        }

        var message = await CreateMessageAsync(fullname, email, $"پایان سرویس | {username}",
            RenderPair(await LoadTemplatePairAsync("FinishServiceAlert"),
                ("{fullname}", fullname, true),
                ("{type}", type, true),
                ("{left}", left, true)));

        await handler.Send(message);
    }

    public async Task SendConnectionEmail(string fullname, string email, ConnectionEmailContext context)
    {
        var bodies = await LoadTemplatePairAsync("ConnectionEmail");

        // sections the account does not own are stripped from both views
        bodies = (
            RenderSection(RenderSection(RenderSection(bodies.Html,
                "password", context.Password), "link", context.Link), "ovpn", context.PrivateKeyOvpn),
            RenderSection(RenderSection(RenderSection(bodies.Text,
                "password", context.Password), "link", context.Link), "ovpn", context.PrivateKeyOvpn));

        IReadOnlyList<EmailAttachment> attachments = context.CertFile is { Length: > 0 }
            ? [new EmailAttachment(context.CertFile, "cert.ovpn", "application/octet-stream")]
            : [];

        var message = await CreateMessageAsync(fullname, email, $"VPN | {fullname}",
            RenderPair(bodies,
                ("{username}", context.Username, true),
                ("{password}", context.Password ?? string.Empty, false),
                ("{link}", context.Link ?? string.Empty, false),
                ("{ovpn}", context.PrivateKeyOvpn ?? string.Empty, false)),
            attachments);

        await handler.Send(message);
    }

    public async Task SendResetPasswordLink(string fullname, string email, string hash_code)
    {
        var message = await CreateMessageAsync(fullname, email, "کد بازیابی",
            RenderPair(await LoadTemplatePairAsync("ResetPassword"),
                ("{code}", hash_code, false)));

        await handler.Send(message);
    }

    public async Task SendWelcomeEmail(string fullname, string email, string username)
    {
        var message = await CreateMessageAsync(fullname, email, $"خوش آمدید | {branding_options.Value.Title}",
            RenderPair(await LoadTemplatePairAsync("Welcome"),
                ("{fullname}", fullname, true),
                ("{username}", username, true)));

        await handler.Send(message);
    }

    public async Task SendRenewalSuccessEmail(string fullname, string email, string plan_title, int price, string expiration)
    {
        var message = await CreateMessageAsync(fullname, email, $"تمدید موفق | {branding_options.Value.Title}",
            RenderPair(await LoadTemplatePairAsync("RenewalSuccess"),
                ("{fullname}", fullname, true),
                ("{plan_title}", plan_title, true),
                ("{price}", price.ToString("N0"), false),
                ("{expiration}", expiration, false)));

        await handler.Send(message);
    }

    /// <summary>
    /// Keeps the <c>&lt;!--if:key--&gt;�&lt;!--end:key--&gt;</c> block (without the
    /// markers) when the section value exists, or removes the whole block
    /// otherwise, so sections the account does not own never reach the user.
    /// </summary>
    private static string RenderSection(string body, string key, string? value)
    {
        var pattern = $@"<!--\s*if:{key}\s*-->(?<content>.*?)<!--\s*end:{key}\s*-->";

        return Regex.Replace(body, pattern,
            string.IsNullOrWhiteSpace(value) ? string.Empty : "${content}",
            RegexOptions.Singleline);
    }
}
