using PhotonBypass.Domain.Session.Model;

namespace PhotonBypass.Domain.Session;

public interface IEmailService
{
    Task SendResetPasswordLink(string fullname, string email, string hash_code);

    Task SendCertEmail(string fullname, string email, CertEmailContext context);

    Task FinishServiceAlert(string fullname, string username, string email, string left);
}
