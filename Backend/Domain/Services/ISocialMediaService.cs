using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Profile;

namespace PhotonBypass.Domain.Services;

public interface ISocialMediaService
{
    Task NewUserRegistrationAlert(AccountEntity account);

    Task SendResetPasswordLink(string email, string hash_code);

    Task InvalidPasswordAlert(string username);

    Task FinishServiceAlert(string username, string phone, PlanType type, string left);

    Task AlarmServerCapacity(IEnumerable<string> alarms);
}
