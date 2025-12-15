using PhotonBypass.Domain.OutSource;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.ServerBridge.Services;
using System.Net.Mail;

namespace PhotonBypass.OutSource;

class EmailService(IEmailHandler handler) : IEmailService
{
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

        var body = await File.ReadAllTextAsync("EmailTemplates/FinishServiceAlert.html");
        body = body.Replace("{fullname}", fullname)
            .Replace("{type}", type)
            .Replace("{left}", left);

        using var message = new MailMessage(from_address, to_address);

        message.Subject = $"پایان سرویس | {username}";
        message.IsBodyHtml = false;
        message.Body = body;

        await handler.Send(message);
    }

    public async Task SendCertEmail(string fullname, string email, CertEmailContext context)
    {
        if (string.IsNullOrWhiteSpace(handler.Options.Address))
        {
            throw new Exception("Email Address is not set in config");
        }

        var from_address = new MailAddress(handler.Options.Address, handler.Options.FullName);
        var to_address = new MailAddress(email, fullname);

        var body = await File.ReadAllTextAsync("EmailTemplates/CertEmail.html");
        body = body.Replace("{username}", context.Username)
            .Replace("{server}", context.Realm)
            .Replace("{password}", context.Password)
            .Replace("{ovpn}", context.PrivateKeyOvpn);

        using var message = new MailMessage(from_address, to_address);
        message.Subject = $"VPN | {fullname}";
        message.IsBodyHtml = false;
        message.Body = body;

        using var stream = new MemoryStream(context.CertFile);

        message.Attachments.Add(new Attachment(stream, "cert.ovpn"));

        await handler.Send(message);
    }

    public async Task SendResetPasswordLink(string fullname, string email, string hash_code)
    {
        if (string.IsNullOrWhiteSpace(handler.Options.Address))
        {
            throw new Exception("Email Address is not set in config");
        }

        var from_address = new MailAddress(handler.Options.Address, handler.Options.FullName);
        var to_address = new MailAddress(email, fullname);

        var body = await File.ReadAllTextAsync("EmailTemplates/ResetPassword.html");
        body = body.Replace("{code}", hash_code);

        using var message = new MailMessage(from_address, to_address);
        message.Subject = $"کد بازیابی";
        message.IsBodyHtml = false;
        message.Body = body;

        await handler.Send(message);
    }
}
