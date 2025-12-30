using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Infra.Radius;

namespace PhotonBypass.FreeRadius.Application;

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

    public Task SyncUserAndActive(ServerEntity radius, AccountEntity account, RenewalEntity renewal)
    {
        throw new NotImplementedException();
    }

    public Task ChangeVpnPassword(ServerEntity radius, string username, string password)
    {
        throw new NotImplementedException();
    }
}