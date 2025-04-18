namespace PhotonBypass.Application;

public interface IEmailHandler
{
    Task SendResetPasswordLink(string email, string hash_code);
}
