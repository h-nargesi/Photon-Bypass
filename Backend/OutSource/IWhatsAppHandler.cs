namespace PhotonBypass.OutSource;

public interface IWhatsAppHandler
{
    Task SendResetPasswordLink(string email, string hash_code);
}
