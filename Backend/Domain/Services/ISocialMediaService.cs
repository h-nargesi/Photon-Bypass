using PhotonBypass.Domain.Account;

namespace PhotonBypass.Domain.Services;

public interface ISocialMediaService
{
    Task NewUserRegistrationAlert(AccountEntity account);

    Task SendResetPasswordLink(string email, string hash_code);

    Task InvalidPasswordAlert(string username);
}
