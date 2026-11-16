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

    public Task FinishServiceAlert(PanelUserEntity account, string plan_title, string remains_title) => Task.CompletedTask;

    public Task InvalidPasswordAlert(PanelUserEntity account) => Task.CompletedTask;

    public Task NewUserRegistrationAlert(PanelUserEntity account) => Task.CompletedTask;

    public Task SendResetPasswordLink(PanelUserEntity account, string hash_code) => Task.CompletedTask;

    public bool Supports(PanelUserEntity account) => false;
}
