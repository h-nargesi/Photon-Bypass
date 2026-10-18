using Microsoft.Extensions.Options;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.ServerBridge.Services;
using System.Collections.Concurrent;
using System.Net.Mail;
using System.Text;
using System.Text.RegularExpressions;

namespace PhotonBypass.OutSource;

class EmailService(IEmailHandler handler, IOptions<BrandingOptions> branding_options) : IEmailService
{
    private const string LogoContentId = "logo";

    private static readonly ConcurrentDictionary<string, Task<string>> TemplateCache = new();
    private static readonly ConcurrentDictionary<string, Task<byte[]>> LogoCache = new();

    private static async Task<string> LoadTemplateAsync(string file_name)
    {
        var task = TemplateCache.GetOrAdd(file_name, path => File.ReadAllTextAsync(path));

        try
        {
            return await task;
        }
        catch
        {
            // a faulted task must not stay cached: a single transient read
            // failure would break every subsequent send until a restart
            TemplateCache.TryRemove(file_name, out _);
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

    private async Task<MailMessage> CreateHtmlMessageAsync(MailAddress from, MailAddress to, string subject, string body)
    {
        var branding = branding_options.Value;
        var logo = await LoadLogoAsync(branding.LogoPath);

        var message = new MailMessage(from, to)
        {
            Subject = subject,
        };

        // The HTML body travels only through the alternate view so the CID
        // logo rides on the same part as the markup; disposing the message
        // disposes the view and its linked-resource stream.
        var view = AlternateView.CreateAlternateViewFromString(body, Encoding.UTF8, "text/html");

        view.LinkedResources.Add(new LinkedResource(new MemoryStream(logo), LogoContentType(branding.LogoPath))
        {
            ContentId = LogoContentId,
        });

        message.AlternateViews.Add(view);

        return message;
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

    public async Task FinishServiceAlert(string fullname, string username, string email, string type, string left)
    {
        if (string.IsNullOrWhiteSpace(handler.Options.Address))
        {
            throw new Exception("Email Address is not set in config");
        }

        if (string.IsNullOrWhiteSpace(fullname))
        {
            fullname = username;
        }

        var from_address = new MailAddress(handler.Options.Address, handler.Options.FullName);
        var to_address = new MailAddress(email, fullname);

        var body = ApplyBranding(await LoadTemplateAsync("EmailTemplates/FinishServiceAlert.html"));
        body = body.Replace("{fullname}", fullname)
            .Replace("{type}", type)
            .Replace("{left}", left);

        using var message = await CreateHtmlMessageAsync(from_address, to_address, $"پایان سرویس | {username}", body);

        await handler.Send(message);
    }

    public async Task SendConnectionEmail(string fullname, string email, ConnectionEmailContext context)
    {
        if (string.IsNullOrWhiteSpace(handler.Options.Address))
        {
            throw new Exception("Email Address is not set in config");
        }

        var from_address = new MailAddress(handler.Options.Address, handler.Options.FullName);
        var to_address = new MailAddress(email, fullname);

        var body = ApplyBranding(await LoadTemplateAsync("EmailTemplates/ConnectionEmail.html"));
        body = RenderSection(body, "password", context.Password);
        body = RenderSection(body, "link", context.Link);
        body = RenderSection(body, "ovpn", context.PrivateKeyOvpn);
        body = body.Replace("{username}", context.Username)
            .Replace("{password}", context.Password ?? string.Empty)
            .Replace("{link}", context.Link ?? string.Empty)
            .Replace("{ovpn}", context.PrivateKeyOvpn ?? string.Empty);

        using var message = await CreateHtmlMessageAsync(from_address, to_address, $"VPN | {fullname}", body);

        if (context.CertFile is { Length: > 0 })
        {
            // the stream must outlive this method: MailMessage disposes its
            // attachments (and their streams) when it is disposed after sending
            message.Attachments.Add(new Attachment(new MemoryStream(context.CertFile), "cert.ovpn"));
        }

        await handler.Send(message);
    }

    /// <summary>
    /// Keeps the <c>&lt;!--if:key--&gt;…&lt;!--end:key--&gt;</c> block (without the
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

    public async Task SendResetPasswordLink(string fullname, string email, string hash_code)
    {
        if (string.IsNullOrWhiteSpace(handler.Options.Address))
        {
            throw new Exception("Email Address is not set in config");
        }

        var from_address = new MailAddress(handler.Options.Address, handler.Options.FullName);
        var to_address = new MailAddress(email, fullname);

        var body = ApplyBranding(await LoadTemplateAsync("EmailTemplates/ResetPassword.html"));
        body = body.Replace("{code}", hash_code);

        using var message = await CreateHtmlMessageAsync(from_address, to_address, "کد بازیابی", body);

        await handler.Send(message);
    }
}
