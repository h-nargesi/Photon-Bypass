using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Domain.Account;

public interface IAccountRadiusSyncService
{
    Task<AccountEntity?> GetUser(string username);

    Task DeactivateUser(IEnumerable<string> usernames);

    Task<bool> SyncUserAndActive(AccountEntity account);

    Task<bool> GetCertificate(IEnumerable<NasEntity> server, string username, CertContext default_context);

    Task<bool> ChangeVpnPassword(string username, string password);

    Task<string> GetVpnPassword(string username);
}
