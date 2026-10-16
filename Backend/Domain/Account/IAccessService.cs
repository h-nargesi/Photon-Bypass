using PhotonBypass.Domain.Account.Model;

namespace PhotonBypass.Domain.Account;

public interface IAccessService
{
    bool CheckAccess(string username, string target);

    void LoginEvent(string username, AccountRole role, IEnumerable<string>? area);

    void LogoutEvent(string username);

    void InvalidateTargetArea(string username);
}
