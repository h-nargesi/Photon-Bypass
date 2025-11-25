using PhotonBypass.Domain.Session.Entity;

namespace PhotonBypass.Domain.Session;

public interface ISocialMediaService
{
    Task NewUserRegistrationAlert(AccountEntity account);

    Task SendResetPasswordLink(string email, string hash_code);

    Task InvalidPasswordAlert(string username);

    Task FinishServiceAlert(string username, string phone, string left);

    Task AlarmServerCapacity(IEnumerable<string> alarms);
}
