using PhotonBypass.Domain.Plan.Entity;

namespace PhotonBypass.Domain.Plan;

public interface ISocialMediaService
{
    Task NewUserRegistrationAlert(AccountEntity account);

    Task SendResetPasswordLink(string email, string hash_code);

    Task InvalidPasswordAlert(string username);

    Task FinishServiceAlert(string username, string phone, string left);

    Task AlarmServerCapacity(IEnumerable<string> alarms);
}
