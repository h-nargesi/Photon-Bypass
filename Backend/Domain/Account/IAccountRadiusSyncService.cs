using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Plan.Entity;

namespace PhotonBypass.Domain.Account;

public interface IAccountRadiusSyncService
{
    Task RemoveUsers(IEnumerable<string> usernames);

    Task DeactivateUsers(IEnumerable<string> usernames);

    Task ActivateUsers(IEnumerable<string> usernames);

    /// <summary>
    /// VPN-valid reconciliation over plan states (per VPN account, docs/22):
    /// only Active accounts belong in the valid set of their own realm —
    /// the bucket-0 realm fan-out is gone because every account pins one realm.
    /// </summary>
    Task DeactivateInvalidRadiusUsers(IEnumerable<PlanStateEntity> plan_state_list);

    /// <summary>
    /// Provisions (or re-provisions and enables) one VPN account on every
    /// active radius server of <see cref="VpnAccountEntity.RealmId"/>.
    /// </summary>
    Task SyncUserAndActive(VpnAccountEntity account, RenewalEntity renewal);

    /// <summary>
    /// D6 realm migration for a stale account: deactivate the username in the
    /// old realm, pin the new realm and provision it there — username and
    /// stacked plan state travel with the account.
    /// </summary>
    Task MigrateUserRealm(VpnAccountEntity account, int new_realm_id, RenewalEntity renewal);

    Task GetOVpnCertificate(int? realm_id, string username, CertContext default_context);

    /// <summary>
    /// Applies a credential change on every active radius server of the realm and
    /// returns the effective credential: for x-ui data-planes the input is ignored
    /// and one freshly generated UUID is applied to all servers (docs/16 D4);
    /// for Radius data-planes the (already applied) password comes back.
    /// </summary>
    Task<string> ChangeVpnPassword(int? realm_id, string username, string password);
}
