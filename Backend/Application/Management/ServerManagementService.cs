using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Services;

namespace PhotonBypass.Application.Management;

class ServerManagementService(IRealmRepository RealmRepo) : IServerManagementService
{
    public async Task<RealmEntity> GetAvalableRealm(int cloud_id)
    {
        var servers = await RealmRepo.FetchServerDensityEntity(cloud_id);

        return servers.Select(x =>
            {
                int.TryParse(x.Capacity, out var capacity);

                return new
                {
                    Realm = x,
                    Capacity = capacity
                };
            })
            .OrderBy(s => s.Realm.UsersCount)
            .ThenByDescending(x => x.Capacity)
            .First()
            .Realm;
    }

    public Task<CertContext> GetDefaultCertificate(string realm)
    {
        throw new NotImplementedException();
    }

    public Task CheckUserServerBalance()
    {
        throw new NotImplementedException();
    }
}
