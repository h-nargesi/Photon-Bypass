using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.Infra.Nas;
using PhotonBypass.Infra.Radius.RadiusDesk;
using PhotonBypass.Infra.Radius.UserManager;
using Serilog;
using OperatingSystem = PhotonBypass.Domain.Servers.Types.OperatingSystem;

namespace PhotonBypass.Infra.Services;

class AccountRadiusSyncService(
    Lazy<IServerRepository> server_repo,
    Lazy<IMikrotikDirectService> mikrotik_direct_srv,
    Lazy<IAccountRadiusSyncUserManagerService> mikrotik_radius,
    Lazy<IAccountRadiusSyncRadiusDeskService> radius_desk)
    : IAccountRadiusSyncService
{
    private Lazy<IServerRepository> ServerRepo { get; } = server_repo;
    private Lazy<IMikrotikDirectService> MikrotikDirectSrv { get; } = mikrotik_direct_srv;
    private Lazy<IAccountRadiusSyncUserManagerService> MikrotikRadius { get; } = mikrotik_radius;
    private Lazy<IAccountRadiusSyncRadiusDeskService> RadiusDesk { get; } = radius_desk;

    public async Task RemoveUsers(IEnumerable<string> usernames)
    {
        var radius_list = await ServerRepo.Value.GetAllActiveRadius();

        await radius_list.RunJob(radius =>
        {
            switch (radius.Features & ServerFeature.Radius)
            {
                case ServerFeature.UserManager:
                    return MikrotikRadius.Value.RemoveUsers(radius, usernames);
                case ServerFeature.RadiusDesk:
                    return RadiusDesk.Value.RemoveUsers(radius, usernames);
                default:
                    Log.Error("Unknown radius-server: (realm-id={0}, radius-id={1}, feature={2})",
                        radius.RealmId, radius.Id, radius.Features);
                    return Task.CompletedTask;
            }
        });
    }

    public Task DeactivateUsers(IEnumerable<string> usernames)
    {
        return DeactivateUsers(usernames, []);
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
            switch (radius.Features & ServerFeature.Radius)
            {
                case ServerFeature.UserManager:
                    return MikrotikRadius.Value.DeactivateUserExcept(radius, realm_user_dictionary[radius.RealmId].ToHashSet());
                case ServerFeature.RadiusDesk:
                    return RadiusDesk.Value.DeactivateUserExcept(radius, realm_user_dictionary[radius.RealmId].ToHashSet());
                default:
                    Log.Error("Unknown radius-server: (realm-id={0}, radius-id={1}, feature={2})",
                        radius.RealmId, radius.Id, radius.Features);
                    return Task.CompletedTask;
            }
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
            switch (radius.Features & ServerFeature.Radius)
            {
                case ServerFeature.UserManager:
                    return MikrotikRadius.Value.SyncUserAndActive(radius, account, renewal);
                case ServerFeature.RadiusDesk:
                    return RadiusDesk.Value.SyncUserAndActive(radius, account, renewal);
                default:
                    Log.Error("Unknown radius-server: (realm-id={0}, radius-id={1}, feature={2})",
                        radius.RealmId, radius.Id, radius.Features);
                    return Task.CompletedTask;
            }
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
                throw new Exception($"Unknown radius-server: (realm-id={master_nas_server.RealmId}, " +
                                    $"radius-id={master_nas_server.Id}, feature={master_nas_server.Features})");
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

        await ChangeVpnPassword(radius_list, username, password);
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

            switch (radius.Features & ServerFeature.Radius)
            {
                case ServerFeature.UserManager:
                    return MikrotikRadius.Value.DeactivateUser(radius, usernames);
                case ServerFeature.RadiusDesk:
                    return RadiusDesk.Value.DeactivateUser(radius, usernames);
                default:
                    Log.Error("Unknown radius-server: (realm-id={0}, radius-id={1}, feature={2})",
                        radius.RealmId, radius.Id, radius.Features);
                    return Task.CompletedTask;
            }
        });
    }

    private async Task ChangeVpnPassword(IEnumerable<ServerEntity> radius_list, string username, string password)
    {
        await radius_list.RunJob(radius =>
        {
            switch (radius.Features & ServerFeature.Radius)
            {
                case ServerFeature.UserManager:
                    return MikrotikRadius.Value.ChangeVpnPassword(radius, username, password);
                case ServerFeature.RadiusDesk:
                    return RadiusDesk.Value.ChangeVpnPassword(radius, username, password);
                default:
                    Log.Error("Unknown radius-server: (realm-id={0}, radius-id={1}, feature={2})",
                        radius.RealmId, radius.Id, radius.Features);
                    return Task.CompletedTask;
            }
        });
    }
}