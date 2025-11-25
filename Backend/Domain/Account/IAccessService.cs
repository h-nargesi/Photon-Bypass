namespace PhotonBypass.Domain.Plan;

public interface IAccessService
{
    bool CheckAccess(string username, string target);

    void LoginEvent(string username, HashSet<string> area);

    void LogoutEvent(string username);
}
