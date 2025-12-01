using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Infra.Radius.RadiusDesk;

namespace PhotonBypass.FreeRadius.Application;

public class AccountRadiusSyncService : IAccountRadiusSyncService
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

    public Task<object> GetCertificate(ServerEntity radius, string username, CertContext default_context)
    {
        throw new NotImplementedException();
    }

    public Task SetCertificate(ServerEntity radius, string username, object certificate)
    {
        throw new NotImplementedException();
    }

    public Task<string?> GetVpnPassword(ServerEntity radius, string username)
    {
        throw new NotImplementedException();
    }

    public Task ChangeVpnPassword(ServerEntity radius, string username, string password)
    {
        throw new NotImplementedException();
    }
}