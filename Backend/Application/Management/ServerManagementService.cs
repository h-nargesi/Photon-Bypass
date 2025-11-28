using Microsoft.Extensions.Options;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using System.Text;
using System.Text.RegularExpressions;

namespace PhotonBypass.Application.Management;

partial class ServerManagementService(
    IRealmRepository RealmRepo,
    ISessionRadiusSyncService SessionRadiusSrv,
    Lazy<IServerRepository> ServerRepo,
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

    public async Task<CertContext> GetDefaultCertificate(int? realm_id)
    {
        var cert_path = Options.Value.DefaultCertPath ??
                        throw new Exception("Default cert-path is not set in config!");

        if (Options.Value.DefaultPrivateKeyOVpn == null)
            throw new Exception("OVpn Private key is not set in config!");

        var realm_name_task = realm_id.HasValue ? RealmRepo.GetName(realm_id.Value) : Task.FromResult<string?>("All");
        var nas_task = ServerRepo.Value.GetAllActiveNasDomainInRealm(realm_id);

        var cert_file = await File.ReadAllBytesAsync(cert_path);

        var nas_list = await nas_task;

        if (nas_list.Count < 1)
            throw new Exception($"Nas/Domain not found: (realm-id={realm_id})!");

        var realm_name = (await realm_name_task) ?? "All";

        var ovpn_conf_file = Encoding.UTF8.GetString(cert_file);
        ovpn_conf_file = SetDomain(ovpn_conf_file, realm_name, nas_list);
        cert_file = Encoding.UTF8.GetBytes(ovpn_conf_file);

        return new CertContext
        {
            Realm = realm_name,
            PrivateKeyOvpn = Options.Value.DefaultPrivateKeyOVpn,
            CertFile = cert_file,
        };
    }

    public async Task CheckUserServerBalance()
    {
        var realms = await LoadServersCapacity();

        var alarms = new List<string>();

        foreach (var realm_pair in realms.Where(r => r.Value.Rate > -1))
        {
            var percent = 100 * realm_pair.Value.Rate;

            switch (percent)
            {
                case < 10:
                    alarms.Add($"Unused Realm: {realm_pair.Key.Name} ({percent:N2}% from {realm_pair.Value.Cap})");
                    break;
                case > 90:
                    alarms.Add($"Low Capacity: {realm_pair.Key.Name} ({percent:N2}% from {realm_pair.Value.Cap})");
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
        var index = DateTime.Now.AddDays(-30);
        var synchronization = SessionRadiusSrv.UpdateTrafficData(index);

        var realms = await RealmRepo.FetchAllActiveRealm();

        var clusters = await ServerRepo.Value.GetAllActiveNasInRealm(realms.Select(r => r.Id));
        var server_ids = clusters.SelectMany(s => s.Value).Select(s => s.Id).ToList();

        await synchronization;
        var traffics = (await TrafficDataRepo.Value.Fetch(server_ids, index))
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