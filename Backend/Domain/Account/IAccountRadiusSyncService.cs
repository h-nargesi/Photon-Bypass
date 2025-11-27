using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Domain.Account;

public interface IAccountRadiusSyncService
{
    Task DeactivateUser(IEnumerable<string> usernames);

    Task<bool> SyncUserAndActive(AccountEntity account, RenewalEntity renewal);

    Task<bool> GetCertificate(int? realm_id, string username, CertContext default_context);

    Task<bool> ChangeVpnPassword(string username, string password);

    Task<string> GetVpnPassword(string username);
}
