using Microsoft.Extensions.Options;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Domain.Services;
using System.Text;
using System.Text.RegularExpressions;

namespace PhotonBypass.Application.Management;

partial class ServerManagementService(
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

        if (Options.Value.Value.DefaultPrivateKeyOvpn == null)
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

        var ovpn_conf_file = Encoding.UTF8.GetString(cert_file);
        ovpn_conf_file = SetDomain(ovpn_conf_file, nas.DomainName);
        cert_file = Encoding.UTF8.GetBytes(ovpn_conf_file);

        return new CertContext
        {
            Server = nas.DomainName,
            PrivateKeyOvpn = Options.Value.Value.DefaultPrivateKeyOvpn,
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

    private static string SetDomain(string cert, string domain)
    {
        cert = SetRemote()
            .Replace(cert, $"remote {domain}");

        cert = SetTitle()
            .Replace(cert, $"setenv FRIENDLY_NAME \"{domain.Split('\'').First()}\"");

        return cert;
    }

    [GeneratedRegex(@"remote ([\w\-])\.photon-bypass\.com")]
    private static partial Regex SetRemote();
    [GeneratedRegex(@"setenv FRIENDLY_NAME ""[^""]+""")]
    private static partial Regex SetTitle();
}
