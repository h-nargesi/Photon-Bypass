using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.OutSource;

namespace PhotonBypass.OutSource;

class SocialMediaService : ISocialMediaService
{
    public Task AlarmServerCapacity(IEnumerable<string> alarms)
    {
        // TODO: Implement
        return Task.CompletedTask;
    }

    public Task FinishServiceAlert(string username, string phone, string type, string left)
    {
        // TODO: Implement
        return Task.CompletedTask;
    }

    public Task InvalidPasswordAlert(string username)
    {
        // TODO: Implement
        return Task.CompletedTask;
    }

    public Task NewUserRegistrationAlert(AccountEntity account)
    {
        // TODO: Implement
        return Task.CompletedTask;
    }

    public Task SendResetPasswordLink(string email, string hash_code)
    {
        // TODO: Implement
        return Task.CompletedTask;
    }
}
