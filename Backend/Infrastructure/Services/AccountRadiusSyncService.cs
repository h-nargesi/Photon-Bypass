using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Infra.Nas;
using PhotonBypass.Infra.Radius;
using Serilog;
using OperatingSystem = PhotonBypass.Domain.Servers.Types.OperatingSystem;

namespace PhotonBypass.Infra.Services;

class AccountRadiusSyncService(
    IServiceProvider provider,
    Lazy<IServerRepository> server_repo,
    Lazy<IMikrotikDirectService> mikrotik_direct_srv)
    : IAccountRadiusSyncService
{
    private IServiceProvider Provider { get; } = provider;
    private Lazy<IServerRepository> ServerRepo { get; } = server_repo;
    private Lazy<IMikrotikDirectService> MikrotikDirectSrv { get; } = mikrotik_direct_srv;

    private Lazy<IInfraAccountRadiusSyncService>? ResolveSync(ServerEntity radius)
    {
        var key = radius.GetSyncKey();

        if (key == null)
        {
            Log.Error("Unknown radius-server: (realm-id={0}, radius-id={1}, feature={2})",
                radius.RealmId, radius.Id, radius.Features);
            return null;
        }

        return Provider.GetRequiredKeyedService<Lazy<IInfraAccountRadiusSyncService>>(key);
    }

    public async Task RemoveUsers(IEnumerable<string> usernames)
    {
        var radius_list = await ServerRepo.Value.GetAllActiveRadius();

        await radius_list.RunJob(radius =>
        {
            var sync = ResolveSync(radius);
            return sync == null ? Task.CompletedTask : sync.Value.RemoveUsers(radius, usernames);
        });
    }

    public Task DeactivateUsers(IEnumerable<string> usernames)
    {
        return DeactivateUsersInRealms(usernames, null);
    }

    public async Task ActivateUsers(IEnumerable<string> usernames)
    {
        var radius_list = await ServerRepo.Value.GetAllActiveRadius();

        await radius_list.RunJob(radius =>
        {
            var sync = ResolveSync(radius);
            return sync == null ? Task.CompletedTask : sync.Value.ActivateUser(radius, usernames);
        });
    }

    public async Task DeactivateInvalidRadiusUsers(IEnumerable<PlanStateEntity> plan_state_list)
    {
        var radius_list = await ServerRepo.Value.GetAllActiveRadius();

        // VPN-valid reconciliation: only Active accounts belong in the valid
        // set — a suspended or deactivated account is disabled on every realm
        // within one sweep, even when the immediate sync failed (Docs/20 D13).
        // Every account pins exactly one realm (Docs/22 §2), so the grouping
        // is a plain RealmId lookup — the bucket-0 fan-out is gone.
        var realm_user_dictionary = plan_state_list
            .Where(plan => plan.Status == VpnAccountStatus.Active)
            .GroupBy(plan => plan.RealmId)
            .ToDictionary(grouping => grouping.Key, grouping => grouping.Select(plan => plan.Username).ToList());

        await radius_list.RunJob(radius =>
        {
            realm_user_dictionary.TryGetValue(radius.RealmId, out var realm_users);
            var valid_users = (realm_users ?? []).ToHashSet();

            var sync = ResolveSync(radius);
            return sync == null ? Task.CompletedTask : sync.Value.DeactivateUserExcept(radius, valid_users);
        });
    }

    public async Task SyncUserAndActive(VpnAccountEntity account, RenewalEntity renewal)
    {
        var radius_list = (await ServerRepo.Value.GetActiveRadiusInRealmOrAll(account.RealmId))
            .Where(radius => radius.RealmId == account.RealmId)
            .ToList();

        if (radius_list.Count <= 0)
        {
            throw new Exception($"No radius server found for realm-id: ({account.RealmId})");
        }

        await radius_list.RunJob(radius =>
        {
            var sync = ResolveSync(radius);
            return sync == null ? Task.CompletedTask : sync.Value.SyncUserAndActive(radius, account, renewal);
        });
    }

    public async Task MigrateUserRealm(VpnAccountEntity account, int new_realm_id, RenewalEntity renewal)
    {
        if (account.RealmId != new_realm_id)
        {
            // D6: the username and the stacked plan state travel to the new
            // realm — the old realm's copy is disabled first so a live client
            // cannot keep authenticating against the stale copy.
            await DeactivateUsersInRealms([account.Username], [account.RealmId]);

            account.RealmId = new_realm_id;
        }

        await SyncUserAndActive(account, renewal);
    }

    public async Task GetOVpnCertificate(int? realm_id, string username, CertContext default_context)
    {
        var nas_list = await ServerRepo.Value.GetActiveNasInRealmOrAll(realm_id);

        if (nas_list.Count <= 0)
        {
            throw new Exception($"No nas server found for realm-id: ({realm_id})");
        }

        var master_nas_server = nas_list[0];

        switch (master_nas_server.OsType)
        {
            case OperatingSystem.Mikrotik:
                await MikrotikDirectSrv.Value.GetOVpnCertificate(master_nas_server, username, default_context);
                break;
            default:
                throw new UserException("کانفیگ اتصال این سرور از نوع OpenVPN نیست؛ از لینک اتصال یا روش اتصال دیگر استفاده کنید.",
                    $"Certificate fetch is not implemented for the nas operating-system: (realm-id={master_nas_server.RealmId}, " +
                    $"nas-id={master_nas_server.Id}, os={master_nas_server.OsType}, feature={master_nas_server.Features})");
        }

        if (nas_list.Count <= 1)
        {
            return;
        }

        await nas_list.Skip(1).RunJob(nas =>
        {
            switch (master_nas_server.OsType)
            {
                case OperatingSystem.Mikrotik:
                    return MikrotikDirectSrv.Value.SetOVpnCertificate(nas, username, default_context);
                default:
                    Log.Error("Unknown radius-server: (realm-id={0}, radius-id={1}, feature={2})",
                        nas.RealmId, nas.Id, nas.Features);
                    return Task.CompletedTask;
            }
        });
    }

    public async Task<string> ChangeVpnPassword(int? realm_id, string username, string password)
    {
        var radius_list = await ServerRepo.Value.GetActiveRadiusInRealmOrAll(realm_id);

        if (radius_list.Count < 1)
        {
            throw new Exception($"No radius server found for realm-id: ({realm_id})");
        }

        var is_xui = radius_list.Any(radius => radius.GetSyncKey() == RadiusType.Xui);
        var is_radius = radius_list.Any(radius => radius.GetSyncKey() != RadiusType.Xui);

        if (is_xui && is_radius)
        {
            // Defensive gate before the fan-out: a mixed realm has no single
            // credential semantics (Docs/17 §5 open item) and RunJob only surfaces
            // failures after every server already applied its change.
            throw new UserException(
                "تغییر شناسه‌ی اتصال برای این سرور پشتیبانی نمی‌شود؛ لطفاً با پشتیبانی تماس بگیرید.",
                $"A mixed data-plane realm (x-ui + Radius) cannot change credentials: realm-id={realm_id}");
        }

        // One credential per request: for x-ui data-planes it is a freshly
        // generated UUID applied to every server (Docs/16 D4 — the dispatcher
        // owns generation, adapters only apply and return); Radius servers
        // apply the caller-supplied password.
        var credential = is_xui ? Guid.NewGuid().ToString() : password;

        var results = await radius_list.RunJob(radius =>
        {
            var sync = ResolveSync(radius);
            return sync == null ? Task.FromResult(credential) : sync.Value.ChangeVpnPassword(radius, username, credential);
        });

        if (results.Any(result => result != credential))
        {
            throw new Exception(
                $"An adapter returned an unexpected credential: realm-id={realm_id}, user={username}");
        }

        return credential;
    }

    private async Task DeactivateUsersInRealms(IEnumerable<string> usernames, IEnumerable<int>? realm_ids)
    {
        var realm_set = realm_ids?.ToHashSet();

        var radius_list = (await ServerRepo.Value.GetAllActiveRadius())
            .Where(radius => realm_set == null || realm_set.Contains(radius.RealmId))
            .ToList();

        await radius_list.RunJob(radius =>
        {
            var sync = ResolveSync(radius);
            return sync == null ? Task.CompletedTask : sync.Value.DeactivateUser(radius, usernames);
        });
    }
}
