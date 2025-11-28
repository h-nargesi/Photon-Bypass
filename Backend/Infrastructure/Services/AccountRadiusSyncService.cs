using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.Infra.Radius.UserManager;
using Serilog;

namespace PhotonBypass.Infra.Services;

class AccountRadiusSyncService(
    Lazy<IPlanStateRepository> PlanRepo,
    Lazy<IServerRepository> ServerRepo,
    Lazy<IAccountRadiusSyncService> MikrotikRadius,
    Lazy<Radius.RadiusDesk.IAccountRadiusSyncService> RadiusDesk)
    : Domain.Account.IAccountRadiusSyncService
{
    public async Task RemoveUsers(IEnumerable<string> usernames)
    {
        var radius_list = await ServerRepo.Value.GetAllActiveRadius();

        await radius_list.RunJob(radius =>
        {
            switch (radius.Features)
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
            switch (radius.Features)
            {
                case ServerFeature.UserManager:
                    return MikrotikRadius.Value.DeactivateUserExcept(radius, realm_user_dictionary[radius.RealmId]);
                case ServerFeature.RadiusDesk:
                    return RadiusDesk.Value.DeactivateUserExcept(radius, realm_user_dictionary[radius.RealmId]);
                default:
                    Log.Error("Unknown radius-server: (realm-id={0}, radius-id={1}, feature={2})",
                        radius.RealmId, radius.Id, radius.Features);
                    return Task.CompletedTask;
            }
        });
    }

    public async Task<bool> SyncUserAndActive(AccountEntity account, RenewalEntity renewal)
    {
        if (renewal.RestrictedRealmId.HasValue)
        {
            await DeactivateUsers([account.Username], [renewal.RestrictedRealmId.Value]);
        }

        var radius_list = await ServerRepo.Value.GetActiveRadiusInRealmOrAll(renewal.RestrictedRealmId);

        if (radius_list.Count <= 0)
        {
            Log.Warning("No radius server found for realm-id: ({0})", renewal.RestrictedRealmId);
            return false;
        }

        var result_list = await radius_list.RunJob(radius =>
        {
            switch (radius.Features)
            {
                case ServerFeature.UserManager:
                    return MikrotikRadius.Value.SyncUserAndActive(radius, account, renewal);
                case ServerFeature.RadiusDesk:
                    return RadiusDesk.Value.SyncUserAndActive(radius, account, renewal);
                default:
                    Log.Error("Unknown radius-server: (realm-id={0}, radius-id={1}, feature={2})",
                        radius.RealmId, radius.Id, radius.Features);
                    return Task.FromResult(false);
            }
        });

        return result_list.Any(r => !r);
    }

    public async Task<bool> GetCertificate(int? realm_id, string username, CertContext default_context)
    {
        var radius_list = await ServerRepo.Value.GetActiveRadiusInRealmOrAll(realm_id);

        if (radius_list.Count <= 0)
        {
            Log.Warning("No radius server found for realm-id: ({0})", realm_id);
            return false;
        }

        var master_radius_server = radius_list[0];

        object cert;
        switch (radius_list[0].Features)
        {
            case ServerFeature.UserManager:
                cert = await MikrotikRadius.Value.GetCertificate(master_radius_server, username, default_context);
                break;
            case ServerFeature.RadiusDesk:
                cert = await RadiusDesk.Value.GetCertificate(master_radius_server, username, default_context);
                break;
            default:
                Log.Error("Unknown radius-server: (realm-id={0}, radius-id={1}, feature={2})",
                    master_radius_server.RealmId, master_radius_server.Id, master_radius_server.Features);
                return false;
        }

        if (radius_list.Count < 2)
        {
            return true;
        }

        var result_list = await radius_list.Skip(1).RunJob(radius =>
        {
            switch (radius.Features)
            {
                case ServerFeature.UserManager:
                    return MikrotikRadius.Value.SetCertificate(radius, username, cert);
                case ServerFeature.RadiusDesk:
                    return RadiusDesk.Value.SetCertificate(radius, username, cert);
                default:
                    Log.Error("Unknown radius-server: (realm-id={0}, radius-id={1}, feature={2})",
                        radius.RealmId, radius.Id, radius.Features);
                    return Task.FromResult(false);
            }
        });

        return result_list.Any(r => !r);
    }

    public async Task<string> GetVpnPassword(int? realm_id, string username)
    {
        var radius_list = await ServerRepo.Value.GetActiveRadiusInRealmOrAll(realm_id);

        if (radius_list.Count <= 0)
        {
            throw new Exception($"No radius server found for realm-id: ({realm_id})");
        }

        var master_radius_server = radius_list[0];

        string result;
        switch (radius_list[0].Features)
        {
            case ServerFeature.UserManager:
                result = await MikrotikRadius.Value.GetVpnPassword(master_radius_server, username);
                break;
            case ServerFeature.RadiusDesk:
                result = await RadiusDesk.Value.GetVpnPassword(master_radius_server, username);
                break;
            default:
                throw new Exception(
                    $"Unknown radius-server: (realm-id={master_radius_server.RealmId}, radius-id={master_radius_server.Id}, feature={master_radius_server.Features})");
        }

        if (radius_list.Count > 1)
        {
            _ = ChangeVpnPassword(radius_list.Skip(1), username, result);
        }

        return result;
    }

    public async Task<bool> ChangeVpnPassword(int? realm_id, string username, string password)
    {
        var radius_list = await ServerRepo.Value.GetActiveRadiusInRealmOrAll(realm_id);

        if (radius_list.Count > 0)
        {
            return await ChangeVpnPassword(radius_list, username, password);
        }

        Log.Warning("No radius server found for realm-id: ({0})", realm_id);
        return false;
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

            switch (radius.Features)
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

    private async Task<bool> ChangeVpnPassword(IEnumerable<ServerEntity> radius_list, string username, string password)
    {
        var result_list = await radius_list.RunJob(radius =>
        {
            switch (radius.Features)
            {
                case ServerFeature.UserManager:
                    return MikrotikRadius.Value.ChangeVpnPassword(radius, username, password);
                case ServerFeature.RadiusDesk:
                    return RadiusDesk.Value.ChangeVpnPassword(radius, username, password);
                default:
                    Log.Error("Unknown radius-server: (realm-id={0}, radius-id={1}, feature={2})",
                        radius.RealmId, radius.Id, radius.Features);
                    return Task.FromResult(false);
            }
        });

        return result_list.Any(r => !r);
    }
}