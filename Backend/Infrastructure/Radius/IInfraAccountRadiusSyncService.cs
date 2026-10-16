using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Infra.Radius;

public interface IInfraAccountRadiusSyncService
{
    Task RemoveUsers(ServerEntity radius, IEnumerable<string> usernames);

    Task DeactivateUserExcept(ServerEntity radius, HashSet<string> usernames);

    Task DeactivateUser(ServerEntity radius, IEnumerable<string> usernames);

    Task ActivateUser(ServerEntity radius, IEnumerable<string> usernames);

    Task SyncUserAndActive(ServerEntity radius, AccountEntity account, RenewalEntity renewal);

    /// <summary>
    /// Applies the credential and returns the effective value: Radius adapters
    /// echo the password they applied; the x-ui adapter applies the UUID passed
    /// in (generated once per request by the dispatcher, docs/16 D4).
    /// </summary>
    Task<string> ChangeVpnPassword(ServerEntity radius, string username, string password);
}