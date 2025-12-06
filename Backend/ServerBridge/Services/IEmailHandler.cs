using System.Net.Mail;

namespace PhotonBypass.ServerBridge.Services;

public interface IEmailHandler
{
    Task Send(MailMessage message);
}
