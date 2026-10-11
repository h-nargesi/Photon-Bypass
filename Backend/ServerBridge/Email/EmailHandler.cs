using Microsoft.Extensions.Options;
using PhotonBypass.ServerBridge.Services;
using System.Net;
using System.Net.Mail;

namespace PhotonBypass.ServerBridge.Email;

class EmailHandler(IOptions<EmailOptions> options) : IEmailHandler
{
    public EmailOptions Options => options.Value;

    public async Task Send(MailMessage message)
    {
        if (string.IsNullOrWhiteSpace(options.Value.Address))
        {
            throw new Exception("Email Address is not set in config");
        }

        if (string.IsNullOrWhiteSpace(options.Value.Password))
        {
            throw new Exception("Email Address is not set in config");
        }

        using var smtp = new SmtpClient
        {
            Host = options.Value.Host,
            Port = options.Value.Port,
            EnableSsl = options.Value.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(options.Value.Address, options.Value.Password)
        };

        using var send_cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.SendTimeoutSeconds));

        try
        {
            await smtp.SendMailAsync(message, send_cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            throw new Exception($"Email send timed out after {options.Value.SendTimeoutSeconds}s.");
        }
    }
}
