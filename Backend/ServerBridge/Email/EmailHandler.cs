using Microsoft.Extensions.Options;
using PhotonBypass.ServerBridge.Services;
using System.Net;
using System.Net.Mail;

namespace PhotonBypass.ServerBridge.Email;

class EmailHandler(IOptions<EmailOptions> options) : IEmailHandler
{
    public EmailOptions Options => options.Value;

    public Task Send(MailMessage message)
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
