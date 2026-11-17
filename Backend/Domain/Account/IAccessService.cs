using PhotonBypass.Domain.Account.Model;

namespace PhotonBypass.Domain.Account;

public interface IAccessService
{
    /// <summary>
    /// Decides whether <paramref name="username"/> may act on behalf of
    /// <paramref name="target"/> (self, a subordinate, or — for admins —
    /// anyone). A cache miss is not a denial: the login area is rebuilt from
    /// the repository so a restart (or a second instance) cannot lock users
    /// out of cross-target access while their JWT is still valid.
    /// </summary>
    Task<bool> CheckAccess(string username, string target);

    /// <summary>
    /// Decides whether <paramref name="username"/> may operate the VPN
    /// account <paramref name="account_id"/> (Docs/22 §3): own account,
    /// a subordinate's account, or — for admins — any account. Same
    /// rebuild-on-miss contract as <see cref="CheckAccess"/>.
    /// </summary>
    Task<bool> CheckAccountAccess(string username, int account_id);

    void LoginEvent(string username, AccountRole role, IEnumerable<string>? area, IEnumerable<int>? accountIds = null);

    void LogoutEvent(string username);

    void InvalidateTargetArea(string username);
}
