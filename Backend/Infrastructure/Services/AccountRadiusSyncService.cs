using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Infra.Services;

class AccountRadiusSyncService : IAccountRadiusSyncService
{
    public Task<AccountEntity?> GetUser(string username)
    {
        throw new NotImplementedException();
    }

    public Task DeactivateUser(IEnumerable<string> usernames)
    {
        throw new NotImplementedException();
    }

    public Task<bool> SyncUserAndActive(AccountEntity account)
    {
        throw new NotImplementedException();
    }

    public Task<bool> CheckUsername(string username)
    {
        throw new NotImplementedException();
    }

    public Task<bool> GetCertificate(IEnumerable<NasEntity> server, string username, CertContext default_context)
    {
        throw new NotImplementedException();
    }

    public Task<bool> ChangeVpnPassword(string username, string password)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetVpnPassword(string username)
    {
        throw new NotImplementedException();
    }
}