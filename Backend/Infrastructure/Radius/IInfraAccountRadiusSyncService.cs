using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Infra.Radius;

public interface IInfraAccountRadiusSyncService
{
    Task RemoveUsers(ServerEntity radius, IEnumerable<string> usernames);

    Task DeactivateUserExcept(ServerEntity radius, HashSet<string> usernames);

    Task DeactivateUser(ServerEntity radius, IEnumerable<string> usernames);

    Task SyncUserAndActive(ServerEntity radius, AccountEntity account, RenewalEntity renewal);

    Task ChangeVpnPassword(ServerEntity radius, string username, string password);
}