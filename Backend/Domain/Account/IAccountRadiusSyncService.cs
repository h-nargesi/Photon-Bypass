using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Domain.Account;

// TODO: Move to Infrastructure
public interface IAccountRadiusSyncService
{
    Task<AccountEntity?> GetUser(string username);

    Task DeactivateUser(IEnumerable<string> usernames);

    Task<bool> SyncUserAndActive(AccountEntity account);

    Task<bool> CheckUsername(string username);

    Task<bool> GetCertificate(IEnumerable<NasEntity> server, string username, CertContext default_context);

    Task<bool> ChangeVpnPassword(string username, string password);

    Task<string> GetVpnPassword(string username);
}
