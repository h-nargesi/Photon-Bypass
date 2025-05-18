using System.Net;
using System.Net.Mail;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Services;

namespace PhotonBypass.OutSource;

class EmailService : IEmailService
{
    private const string EMAIL_NAME = "Ryan Nargesi";
    private const string EMAIL_ADDRESS = "ryan.nargesi.ai@gmail.com";
    private const string PASSWORD = "fromPassword";

    public Task FinishServiceAlert(string fullname, string username, string email, PlanType type, string left)
    {
        if (string.IsNullOrWhiteSpace(fullname))
        {
            fullname = username;
        }

        var fromAddress = new MailAddress(EMAIL_ADDRESS, EMAIL_NAME);
        var toAddress = new MailAddress(email, fullname);

        using var message = new MailMessage(fromAddress, toAddress)
        {
            Subject = $"پایان سرویس | {username}",
            IsBodyHtml = false,
            Body = @$"
اکانت شما بزودی پایان می‌یابد:
نام: {username} | {type}
باقیمانده: {left}
"
        };

        return Send(message);
    }

    public Task SendCertEmail(string fullname, string email, CertEmailContext context)
    {
        var fromAddress = new MailAddress(EMAIL_ADDRESS, EMAIL_NAME);
        var toAddress = new MailAddress(email, fullname);

        using var message = new MailMessage(fromAddress, toAddress)
        {
            Subject = $"VPN | {fullname}",
            IsBodyHtml = false,
            Body = @$"
User = {context.Username}
Password = {context.Password}
---------------------------------------------------
L2TP: L2TP/IPSec PSK (Pre-Shared Key)
Server = {context.Server}
Pre-shared key = {context.PrivateKeyL2TP}
---------------------------------------------------
OpenVPN (Password with Certificates)
User key password = {context.PrivateKeyOvpn}
Attached config.ovpn file
---------------------------------------------------
 User Guides
 OpenVPN Installer
            "
        };
        
        using var stream = new MemoryStream(context.CertFile);

        message.Attachments.Add(new Attachment(stream, "cert.ovpn"));

        return Send(message);
    }

    public Task SendResetPasswordLink(string fullname, string email, string hash_code)
    {
        var fromAddress = new MailAddress(EMAIL_ADDRESS, EMAIL_NAME);
        var toAddress = new MailAddress(email, fullname);

        using var message = new MailMessage(fromAddress, toAddress)
        {
            Subject = $"کد بازیابی",
            IsBodyHtml = false,
            Body = @$"کد بازیابی: hash_code"
        };

        return Send(message);
    }

    private static Task Send(MailMessage message)
    {
        var smtp = new SmtpClient
        {
            Host = "smtp.gmail.com",
            Port = 587,
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(EMAIL_ADDRESS, PASSWORD)
        };

        return smtp.SendMailAsync(message);
    }
}
