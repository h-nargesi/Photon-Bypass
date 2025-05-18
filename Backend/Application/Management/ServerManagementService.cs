using Microsoft.Extensions.Options;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Domain.Services;

namespace PhotonBypass.Application.Management;

class ServerManagementService(
    IRealmRepository RealmRepo,
    Lazy<INasRepository> NasRepo,
    Lazy<IOptions<ManagementOptions>> Options)
    : IServerManagementService
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

    public async Task<CertContext> GetDefaultCertificate(int realmid)
    {
        var cxert_path = Options.Value.Value.DefaultCertPath ??
            throw new Exception("Default cert-path is not set in config!");

        if (Options.Value.Value.PrivateKey == null)
            throw new Exception("Private key is not set in config!");

        var realm_task = RealmRepo.Fetch(realmid);

        var cert_file = await File.ReadAllBytesAsync(cxert_path);

        var realm = (await realm_task) ??
            throw new Exception($"Realm not found: ({realmid})!");

        if (realm.RestrictedServerIP == null)
        {
            throw new Exception($"Realm does not have server-ip: ({realmid})!");
        }

        var nas = await NasRepo.Value.GetNasInfo(realm.RestrictedServerIP);

        if (nas?.DomainName == null)
        {
            throw new Exception($"Nas/Domain not found: ({realm.RestrictedServerIP}, nas-id={nas?.Id})!");
        }

        return new CertContext
        {
            Server = nas.DomainName,
            PrivateKey = Options.Value.Value.PrivateKey,
            CertFile = cert_file,
        };
    }

    public Task CheckUserServerBalance()
    {
        throw new NotImplementedException();
    }
}
