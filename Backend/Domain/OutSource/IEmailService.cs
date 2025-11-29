using PhotonBypass.Domain.OutSource.Model;

namespace PhotonBypass.Domain.OutSource;

public interface IEmailService
{
    Task SendResetPasswordLink(string fullname, string email, string hash_code);

    Task SendCertEmail(string fullname, string email, CertEmailContext context);

    Task FinishServiceAlert(string fullname, string username, string email, string type, string left);
}
