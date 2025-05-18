using Microsoft.Extensions.Options;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Domain.Services;

namespace PhotonBypass.Application.Management;

class ServerManagementService(
    IRealmRepository RealmRepo,
    Lazy<INasRepository> NasRepo,
    Lazy<ICloudRepository> CloudRepo,
    Lazy<ISocialMediaService> SocialSrv,
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

        if (Options.Value.Value.PrivateKeyL2TP == null)
            throw new Exception("L2TP Private key is not set in config!");

        if (Options.Value.Value.PrivateKeyOvpn == null)
            throw new Exception("Ovpn Private key is not set in config!");

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
            PrivateKeyOvpn = Options.Value.Value.PrivateKeyOvpn,
            PrivateKeyL2TP = Options.Value.Value.PrivateKeyL2TP,
            CertFile = cert_file,
        };
    }

    public async Task CheckUserServerBalance()
    {
        var web_cloud_id = await CloudRepo.Value.FindWebCloud();
        var servers = await RealmRepo.FetchServerDensityEntity(web_cloud_id);

        var alarms = new List<string>();

        foreach (var server in servers)
        {
            if (!float.TryParse(server.Capacity, out var capacity))
            {
                continue;
            }

            var percent = 100 * capacity / server.UsersCount;

            if (percent < 10)
            {
                alarms.Add($"Unused Server: {server.RestrictedServerIP} ({percent}% from {server.UsersCount})");
            }
            else if (percent > 90)
            {
                alarms.Add($"Low Capacity: {server.RestrictedServerIP} ({percent}% from {server.UsersCount})");
            }
        }

        if (alarms.Count != 0)
        {
            await SocialSrv.Value.AlarmServerCapacity(alarms);
        }
    }
}
