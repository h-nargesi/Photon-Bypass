using PhotonBypass.ServerBridge.Email;

namespace PhotonBypass.ServerBridge.Services;

public interface IEmailHandler
{
    EmailOptions Options { get; }

    Task Send(EmailMessage message);
}
