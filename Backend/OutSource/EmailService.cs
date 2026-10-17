using PhotonBypass.Domain.OutSource;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.ServerBridge.Services;
using System.Collections.Concurrent;
using System.Net.Mail;
using System.Text.RegularExpressions;

namespace PhotonBypass.OutSource;

class EmailService(IEmailHandler handler) : IEmailService
{
    private static readonly ConcurrentDictionary<string, Task<string>> TemplateCache = new();

    private static Task<string> LoadTemplateAsync(string file_name)
    {
        return TemplateCache.GetOrAdd(file_name, path => File.ReadAllTextAsync(path));
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

        var body = await LoadTemplateAsync("EmailTemplates/FinishServiceAlert.html");
        body = body.Replace("{fullname}", fullname)
            .Replace("{type}", type)
            .Replace("{left}", left);

        using var message = new MailMessage(from_address, to_address);

        message.Subject = $"پایان سرویس | {username}";
        message.IsBodyHtml = false;
        message.Body = body;

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

        var body = await LoadTemplateAsync("EmailTemplates/ConnectionEmail.html");
        body = RenderSection(body, "password", context.Password);
        body = RenderSection(body, "link", context.Link);
        body = RenderSection(body, "ovpn", context.PrivateKeyOvpn);
        body = body.Replace("{username}", context.Username)
            .Replace("{password}", context.Password ?? string.Empty)
            .Replace("{link}", context.Link ?? string.Empty)
            .Replace("{ovpn}", context.PrivateKeyOvpn ?? string.Empty);

        using var message = new MailMessage(from_address, to_address);
        message.Subject = $"VPN | {fullname}";
        message.IsBodyHtml = false;
        message.Body = body;

        if (context.CertFile is { Length: > 0 })
        {
            using var stream = new MemoryStream(context.CertFile);

            message.Attachments.Add(new Attachment(stream, "cert.ovpn"));
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

        var body = await LoadTemplateAsync("EmailTemplates/ResetPassword.html");
        body = body.Replace("{code}", hash_code);

        using var message = new MailMessage(from_address, to_address);
        message.Subject = $"کد بازیابی";
        message.IsBodyHtml = false;
        message.Body = body;

        await handler.Send(message);
    }
}
