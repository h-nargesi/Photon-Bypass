using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Plan.Entity;

namespace PhotonBypass.Domain.Account;

public interface IAccountRadiusSyncService
{
    Task RemoveUsers(IEnumerable<string> usernames);

    Task DeactivateUsers(IEnumerable<string> usernames);

    Task ActivateUsers(IEnumerable<string> usernames);

    Task DeactivateInvalidRadiusUsers(IEnumerable<PlanStateEntity> plan_state_list);

    Task SyncUserAndActive(AccountEntity account, RenewalEntity renewal);

    Task GetOVpnCertificate(int? realm_id, string username, CertContext default_context);

    /// <summary>
    /// Applies a credential change on every active radius server of the realm and
    /// returns the effective credential: for x-ui data-planes the input is ignored
    /// and one freshly generated UUID is applied to all servers (docs/16 D4);
    /// for Radius data-planes the (already applied) password comes back.
    /// </summary>
    Task<string> ChangeVpnPassword(int? realm_id, string username, string password);
}
