using PhotonBypass.Domain.Session;
using PhotonBypass.Domain.Session;
using PhotonBypass.Domain.Session;

namespace PhotonBypass.OutSource;

class SocialMediaService : ISocialMediaService
{
    public Task AlarmServerCapacity(IEnumerable<string> alarms)
    {
        throw new NotImplementedException();
    }

    public Task FinishServiceAlert(string username, string phone, PlanType type, string left)
    {
        throw new NotImplementedException();
    }

    public Task InvalidPasswordAlert(string username)
    {
        throw new NotImplementedException();
    }

    public Task NewUserRegistrationAlert(AccountEntity account)
    {
        throw new NotImplementedException();
    }

    public Task SendResetPasswordLink(string email, string hash_code)
    {
        throw new NotImplementedException();
    }
}
