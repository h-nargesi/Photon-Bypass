using PhotonBypass.Domain.Account.Entity;

namespace PhotonBypass.Domain.Account;

public interface ISocialMediaService
{
    Task NewUserRegistrationAlert(AccountEntity account);

    Task SendResetPasswordLink(string email, string hash_code);

    Task InvalidPasswordAlert(string username);

    Task FinishServiceAlert(string username, string phone, string left);

    Task AlarmServerCapacity(IEnumerable<string> alarms);
}
