using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.Infra.MikrotikRadius;
using PhotonBypass.Infra.Repository;
using Serilog;

namespace PhotonBypass.Infra.Services;

class AccountRadiusSyncService(
    Lazy<PlanStateRepository> PlanRepo,
    Lazy<ServerRepository> ServerRepo,
    Lazy<IAccountRadiusSyncService> MikrotikRadius,
    Lazy<RadiusDesk.IAccountRadiusSyncService> RadiusDesk)
    : Domain.Account.IAccountRadiusSyncService
{
    public async Task DeactivateUser(IEnumerable<string> usernames)
    {
        var radius_list = await ServerRepo.Value.GetAllActiveRadiusInRealm((int?)null);

        await radius_list.RunJob(radius =>
        {
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

    public Task<bool> SyncUserAndActive(AccountEntity account, RenewalEntity renewal)
    {
        throw new NotImplementedException();
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
}