using PhotonBypass.ServerBridge.Email;
using System.Net.Mail;

namespace PhotonBypass.ServerBridge.Services;

public interface IEmailHandler
{
    EmailOptions Options { get; }

    Task Send(MailMessage message);
}
