using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Infra.RadiusDesk;

public interface IAccountRadiusSyncService
{
    Task RemoveUsers(ServerEntity radius, IEnumerable<string> usernames);
      
    Task DeactivateUserExcept(ServerEntity radius, IEnumerable<string> usernames);

    Task DeactivateUser(ServerEntity radius, IEnumerable<string> usernames);

    Task<bool> SyncUserAndActive(ServerEntity radius, AccountEntity account, RenewalEntity renewal);
}