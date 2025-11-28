using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Domain.Account;

public interface IAccountRadiusSyncService
{
    Task RemoveUsers(IEnumerable<string> usernames);

    Task DeactivateUsers(IEnumerable<string> usernames);
    
    Task DeactivateInvalidRadiusUsers(IEnumerable<PlanStateEntity> plan_state_list);

    Task<bool> SyncUserAndActive(AccountEntity account, RenewalEntity renewal);

    Task<bool> GetCertificate(int? realm_id, string username, CertContext default_context);

    Task<string> GetVpnPassword(int? realm_id, string username);

    Task<bool> ChangeVpnPassword(int? realm_id, string username, string password);
}
