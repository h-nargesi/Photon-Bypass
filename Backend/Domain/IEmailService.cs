namespace PhotonBypass.Domain;

public interface IEmailService
{
    Task SendResetPasswordLink(string email, string hash_code);
}
