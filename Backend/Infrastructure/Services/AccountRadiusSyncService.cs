using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
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
        return DeactivateUsers(usernames, []);
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
        var realm_user_dictionary = plan_state_list.GroupBy(plan => plan.RestrictedRealmId ?? 0)
            .ToDictionary(grouping => grouping.Key, grouping => grouping.Select(plan => plan.Username).ToList());

        if (realm_user_dictionary.TryGetValue(0, out var list))
        {
            foreach (var pair in realm_user_dictionary.Where(pair => pair.Key != 0))
            {
                pair.Value.AddRange(list);
            }
        }

        await radius_list.RunJob(radius =>
        {
            realm_user_dictionary.TryGetValue(radius.RealmId, out var realm_users);
            var valid_users = (realm_users ?? []).ToHashSet();

            var sync = ResolveSync(radius);
            return sync == null ? Task.CompletedTask : sync.Value.DeactivateUserExcept(radius, valid_users);
        });
    }

    public async Task SyncUserAndActive(AccountEntity account, RenewalEntity renewal)
    {
        if (renewal.RestrictedRealmId.HasValue)
        {
            await DeactivateUsers([account.Username], [renewal.RestrictedRealmId.Value]);
        }

        var radius_list = await ServerRepo.Value.GetActiveRadiusInRealmOrAll(renewal.RestrictedRealmId);

        if (radius_list.Count <= 0)
        {
            throw new Exception($"No radius server found for realm-id: ({renewal.RestrictedRealmId})");
        }

        await radius_list.RunJob(radius =>
        {
            var sync = ResolveSync(radius);
            return sync == null ? Task.CompletedTask : sync.Value.SyncUserAndActive(radius, account, renewal);
        });
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

    public async Task ChangeVpnPassword(int? realm_id, string username, string password)
    {
        var radius_list = await ServerRepo.Value.GetActiveRadiusInRealmOrAll(realm_id);

        if (radius_list.Count < 1)
        {
            throw new Exception($"No radius server found for realm-id: ({realm_id})");
        }

        await radius_list.RunJob(radius =>
        {
            var sync = ResolveSync(radius);
            return sync == null ? Task.CompletedTask : sync.Value.ChangeVpnPassword(radius, username, password);
        });
    }

    private async Task DeactivateUsers(IEnumerable<string> usernames, HashSet<int> realm_exceptions)
    {
        var radius_list = await ServerRepo.Value.GetAllActiveRadius();

        await radius_list.RunJob(radius =>
        {
            if (realm_exceptions.Contains(radius.RealmId))
            {
                return Task.CompletedTask;
            }

            var sync = ResolveSync(radius);
            return sync == null ? Task.CompletedTask : sync.Value.DeactivateUser(radius, usernames);
        });
    }
}
