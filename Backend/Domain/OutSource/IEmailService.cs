using PhotonBypass.Domain.OutSource.Model;

namespace PhotonBypass.Domain.OutSource;

public interface IEmailService
{
    Task SendResetPasswordLink(string fullname, string email, string hash_code);

    Task SendConnectionEmail(string fullname, string email, ConnectionEmailContext context);

    Task FinishServiceAlert(string fullname, string username, string email, string type, string left);

    Task SendWelcomeEmail(string fullname, string email, string username);

    Task SendRenewalSuccessEmail(string fullname, string email, string plan_title, int price, string expiration);
}
