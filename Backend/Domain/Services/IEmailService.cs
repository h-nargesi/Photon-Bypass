using PhotonBypass.Domain.Profile;

namespace PhotonBypass.Domain.Services;

public interface IEmailService
{
    Task SendResetPasswordLink(string fullname, string email, string hash_code);

    Task SendCertEmail(string fullname, string email, CertEmailContext context);

    Task FinishServiceAlert(string fullname, string username, string email, PlanType type, string left);
}
