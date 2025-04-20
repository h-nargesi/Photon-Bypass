namespace PhotonBypass.Domain.Services;

public interface IEmailService
{
    Task SendResetPasswordLink(string email, string hash_code);

    Task SendCertEmail(string email, CertEmailContext context);
}
