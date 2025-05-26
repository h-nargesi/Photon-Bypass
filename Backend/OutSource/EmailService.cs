using Microsoft.Extensions.Options;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Services;
using System.Net;
using System.Net.Mail;

namespace PhotonBypass.OutSource;

class EmailService(IOptions<EmailOptions> options) : IEmailService
{
    public async Task FinishServiceAlert(string fullname, string username, string email, PlanType type, string left)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Address))
        {
            throw new Exception("Email Address is not set in config");
        }

        if (string.IsNullOrWhiteSpace(fullname))
        {
            fullname = username;
        }

        var fromAddress = new MailAddress(options.Value.Address, options.Value.FullName);
        var toAddress = new MailAddress(email, fullname);

        var body = await File.ReadAllTextAsync("EmailTemplates\\FinishServiceAlert.html");
        body = body.Replace("{fullname}", fullname)
            .Replace("{type}", type.ToString())
            .Replace("{left}", left);

        using var message = new MailMessage(fromAddress, toAddress)
        {
            Subject = $"پایان سرویس | {username}",
            IsBodyHtml = false,
            Body = body,
        };

        await Send(message);
    }

    public async Task SendCertEmail(string fullname, string email, CertEmailContext context)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Address))
        {
            throw new Exception("Email Address is not set in config");
        }

        var fromAddress = new MailAddress(options.Value.Address, options.Value.FullName);
        var toAddress = new MailAddress(email, fullname);

        var body = await File.ReadAllTextAsync("EmailTemplates\\CertEmail.html");
        body = body.Replace("{username}", context.Username)
            .Replace("{server}", context.Server)
            .Replace("{password}", context.Password)
            .Replace("{ovpn}", context.PrivateKeyOvpn);

        using var message = new MailMessage(fromAddress, toAddress)
        {
            Subject = $"VPN | {fullname}",
            IsBodyHtml = false,
            Body = body,
        };
        
        using var stream = new MemoryStream(context.CertFile);

        message.Attachments.Add(new Attachment(stream, "cert.ovpn"));

        await Send(message);
    }

    public async Task SendResetPasswordLink(string fullname, string email, string hash_code)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Address))
        {
            throw new Exception("Email Address is not set in config");
        }

        var fromAddress = new MailAddress(options.Value.Address, options.Value.FullName);
        var toAddress = new MailAddress(email, fullname);

        var body = await File.ReadAllTextAsync("EmailTemplates\\ResetPassword.html");
        body = body.Replace("{code}", hash_code);

        using var message = new MailMessage(fromAddress, toAddress)
        {
            Subject = $"کد بازیابی",
            IsBodyHtml = false,
            Body = body,
        };

        await Send(message);
    }

    private Task Send(MailMessage message)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Address))
        {
            throw new Exception("Email Address is not set in config");
        }

        if (string.IsNullOrWhiteSpace(options.Value.Password))
        {
            throw new Exception("Email Address is not set in config");
        }

        var smtp = new SmtpClient
        {
            Host = "smtp.gmail.com",
            Port = 587,
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(options.Value.Address, options.Value.Password)
        };

        return smtp.SendMailAsync(message);
    }
}
