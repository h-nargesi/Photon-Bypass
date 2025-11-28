using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.Infra.Repository;
using Serilog;

namespace PhotonBypass.Infra.Services;

class AccountRadiusSyncService(
    Lazy<PlanStateRepository> PlanRepo,
    Lazy<ServerRepository> ServerRepo,
    Lazy<MikrotikRadius.IAccountRadiusSyncService> MikrotikRadius,
    Lazy<RadiusDesk.IAccountRadiusSyncService> RadiusDesk)
    : IAccountRadiusSyncService
{
    public async Task RemoveUsers(IEnumerable<string> usernames)
    {
        var radius_list = await ServerRepo.Value.GetAllActiveRadiusInRealm((int?)null);

        await radius_list.RunJob(radius =>
        {
            switch (radius.Features)
            {
                case ServerFeature.UserManager:
                    return MikrotikRadius.Value.DeleteUser(radius, usernames);
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
        var radius_list = await ServerRepo.Value.GetAllActiveRadiusInRealm((int?)null);
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

        var radius_list = await ServerRepo.Value.GetAllActiveRadiusInRealm(renewal.RestrictedRealmId);

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

    public Task<bool> GetCertificate(int? realm_id, string username, CertContext default_context)
    {
        throw new NotImplementedException();
    }

    public Task<bool> ChangeVpnPassword(string username, string password)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetVpnPassword(string username)
    {
        throw new NotImplementedException();
    }

    private async Task DeactivateUsers(IEnumerable<string> usernames, HashSet<int> realm_exceptions)
    {
        var radius_list = await ServerRepo.Value.GetAllActiveRadiusInRealm((int?)null);

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
}