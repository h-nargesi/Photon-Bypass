using Microsoft.Extensions.Options;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Types;
using System.Text;
using System.Text.RegularExpressions;
using PhotonBypass.Domain.Plan;

namespace PhotonBypass.Application.Management;

partial class ServerManagementService(
    IRealmRepository RealmRepo,
    ISessionRadiusSyncService RadiusSrv,
    Lazy<INasRepository> NasRepo,
    Lazy<ITrafficDataRepository> TrafficDataRepo,
    Lazy<ISocialMediaService> SocialSrv,
    IOptions<ManagementOptions> Options)
    : IServerManagementService
{
    public async Task<RealmEntity> GetAvailableRealm()
    {
        return (await LoadServersCapacity())
            .Where(realm => realm.Value.Rate > -1)
            .OrderBy(s => s.Value)
            .First()
            .Key;
    }

    public async Task<CertContext> GetDefaultCertificate(int realm_id)
    {
        var cert_path = Options.Value.DefaultCertPath ??
                        throw new Exception("Default cert-path is not set in config!");

        if (Options.Value.DefaultPrivateKeyOVpn == null)
            throw new Exception("OVpn Private key is not set in config!");

        var realm_task = RealmRepo.Fetch(realm_id);
        var servers_task = NasRepo.Value.GetAllDomainInRealm(realm_id);

        var cert_file = await File.ReadAllBytesAsync(cert_path);

        var realm = await realm_task ??
                    // TODO: test should throw an exception
                    throw new Exception($"Realm not found: (realm-id={realm_id})!");

        var servers = await servers_task ??
                      // TODO: test should throw an exception
                      throw new Exception($"Nas/Domain not found: (realm-id={realm_id})!");

        var ovpn_conf_file = Encoding.UTF8.GetString(cert_file);
        ovpn_conf_file = SetDomain(ovpn_conf_file, realm.Name, servers);
        cert_file = Encoding.UTF8.GetBytes(ovpn_conf_file);

        return new CertContext
        {
            Realm = realm.Name,
            PrivateKeyOvpn = Options.Value.DefaultPrivateKeyOVpn,
            CertFile = cert_file,
        };
    }

    public async Task CheckUserServerBalance()
    {
        var realms = await LoadServersCapacity();

        var alarms = new List<string>();

        foreach (var realm in realms.Where(r => r.Value.Rate > -1))
        {
            var percent = 100 * realm.Value.Rate;

            switch (percent)
            {
                case < 10:
                    alarms.Add($"Unused Realm: {realm.Key.Name} ({percent:N2}% from {realm.Value.Cap})");
                    break;
                case > 90:
                    alarms.Add($"Low Capacity: {realm.Key.Name} ({percent:N2}% from {realm.Value.Cap})");
                    break;
            }
        }

        if (alarms.Count != 0)
        {
            await SocialSrv.Value.AlarmServerCapacity(alarms);
        }
    }

    private async Task<Dictionary<RealmEntity, (double Rate, long Cap)>> LoadServersCapacity()
    {
        var realms = await RealmRepo.FetchAllActiveRealm();

        var clusters = await NasRepo.Value.GetAllInRealm(realms.Select(r => r.Id));
        var servers = clusters.SelectMany(s => s.Value).ToList();

        await RadiusSrv.UpdateTrafficData(servers);

        var traffics = (await TrafficDataRepo.Value.Fetch(servers.Select(s => s.Id), DateTime.Now.AddDays(-30)))
            .ToDictionary(k =>
                k.Key, v =>
                v.Value.Select(t => (t.StartSession, t.TotalData))
                    .GroupBy(k => k.StartSession)
                    .ToDictionary(k => k.Key, a => a.Select(x => x.TotalData).Sum()));

        // TODO: Use IQR to find real average
        var real_traffics = traffics.ToDictionary(k => k.Key, v => v.Value.Values.Average());

        return clusters.Select(cluster =>
            {
                var usage = 0D;
                var capacity = 0L;

                foreach (var server in cluster.Value)
                {
                    capacity += server.BandWidth;
                    if (real_traffics.TryGetValue(server.Id, out var data_usage))
                    {
                        usage += data_usage;
                    }
                }

                return new
                {
                    Realm = realms[cluster.Key],
                    Capacity = capacity,
                    UsageRate = capacity == 0 ? -1 : usage / capacity,
                };
            })
            .ToDictionary(k => k.Realm, v => (v.UsageRate, v.Capacity));
    }

    private static string SetDomain(string cert, string name, IEnumerable<string> domains)
    {
        var remotes = "remote " + string.Join("\nremote ", domains);
        cert = SetRemote()
            .Replace(cert, remotes);

        cert = SetTitle()
            .Replace(cert, $"setenv FRIENDLY_NAME \"{name}\"");

        return cert;
    }

    [GeneratedRegex(@"remote ([\w\-])\.photon-bypass\.com")]
    private static partial Regex SetRemote();

    [GeneratedRegex(@"setenv FRIENDLY_NAME ""[^""]+""")]
    private static partial Regex SetTitle();
}