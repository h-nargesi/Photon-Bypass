using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.OutSource;

namespace PhotonBypass.OutSource;

/// <summary>
/// The <see cref="SocialChannel.None"/> adapter — the replacement for the old
/// stub: notifications are no-ops and no account is reachable, so callers
/// fall back to their email paths on a fresh install.
/// </summary>
internal sealed class NoopSocialMediaService : ISocialMediaService
{
    public Task AlarmServerCapacity(IReadOnlyList<string> alarms) => Task.CompletedTask;

    public Task FinishServiceAlert(AccountEntity account, string plan_title, string remains_title) => Task.CompletedTask;

    public Task InvalidPasswordAlert(AccountEntity account) => Task.CompletedTask;

    public Task NewUserRegistrationAlert(AccountEntity account) => Task.CompletedTask;

    public Task SendResetPasswordLink(AccountEntity account, string hash_code) => Task.CompletedTask;

    public bool Supports(AccountEntity account) => false;
}
