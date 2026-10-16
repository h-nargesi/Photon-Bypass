using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Infra.Radius;

namespace PhotonBypass.RadiusDesk.Application;

public class AccountRadiusSyncRadiusDeskService : IInfraAccountRadiusSyncService
{
    public Task RemoveUsers(ServerEntity radius, IEnumerable<string> usernames)
    {
        throw new NotImplementedException();
    }

    public Task DeactivateUserExcept(ServerEntity radius, HashSet<string> usernames)
    {
        throw new NotImplementedException();
    }

    public Task DeactivateUser(ServerEntity radius, IEnumerable<string> usernames)
    {
        throw new NotImplementedException();
    }

    public Task ActivateUser(ServerEntity radius, IEnumerable<string> usernames)
    {
        throw new NotImplementedException();
    }

    public Task SyncUserAndActive(ServerEntity radius, AccountEntity account, RenewalEntity renewal)
    {
        throw new NotImplementedException();
    }

    public Task<string> ChangeVpnPassword(ServerEntity radius, string username, string password)
    {
        // Deferred with the rest of the RadiusDesk adapter (docs/14 L8): echo the
        // effective-credential contract instead of throwing a landmine, so the
        // dispatcher path stays uniform the day the adapter is implemented.
        return Task.FromResult(password);
    }
}