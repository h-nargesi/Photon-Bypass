namespace PhotonBypass.Domain;

public interface ISocialMediaService
{
    Task SendResetPasswordLink(string email, string hash_code);

    Task SendInvalidPasswordAlert(string username);
}
