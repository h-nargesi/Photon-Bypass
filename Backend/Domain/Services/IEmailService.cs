using PhotonBypass.Domain.Profile;

namespace PhotonBypass.Domain.Services;

public interface IEmailService
{
    Task SendResetPasswordLink(string email, string hash_code);

    Task SendCertEmail(string email, CertEmailContext context);

    Task FinishServiceAlert(string username, string email, PlanType type, string left);
}
