using Microsoft.Extensions.Options;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using Serilog;
using System.Text;
using System.Text.RegularExpressions;

namespace PhotonBypass.Application.Management;

partial class ServerManagementService(
    IRealmRepository realm_repo,
    ITrafficDataRepository traffic_data_repo,
    Lazy<ISessionRadiusSyncService> session_radius_srv,
    Lazy<IServerRepository> server_repo,
    Lazy<IAccountRepository> account_repo,
    Lazy<ISocialMediaService> social_srv,
    IOptions<ManagementOptions> options)
    : IServerManagementService
{
    private IRealmRepository RealmRepo { get; } = realm_repo;
    private ITrafficDataRepository TrafficDataRepo { get; } = traffic_data_repo;
    private Lazy<ISessionRadiusSyncService> SessionRadiusSrv { get; } = session_radius_srv;
    private Lazy<IServerRepository> ServerRepo { get; } = server_repo;
    private Lazy<IAccountRepository> AccountRepo { get; } = account_repo;
    private Lazy<ISocialMediaService> SocialSrv { get; } = social_srv;
    private IOptions<ManagementOptions> Options { get; } = options;

    public async Task<RealmEntity> GetAvailableRealm()
    {
        var server_capacities = await LoadServersCapacity();

        return server_capacities
            .Where(realm => realm.Value.Rate > -1)
            .OrderBy(s => s.Value.Rate)
            .First()
            .Key;
    }

    public async Task<CertContext> GetDefaultCertificate(int? realm_id)
    {
        var cert_path = Options.Value.DefaultCertPath ??
                        throw new Exception("Default cert-path is not set in config!");

        if (Options.Value.DefaultPrivateKeyOVpn == null)
            throw new Exception("OVpn Private key is not set in config!");

        var realm_name = (realm_id.HasValue ? (await RealmRepo.GetName(realm_id.Value)) : null) ?? "All";
        var nas_domain_task = ServerRepo.Value.GetAllActiveNasDomainInRealm(realm_id);

        var cert_file = await File.ReadAllBytesAsync(cert_path);

        var nas_domain_list = await nas_domain_task;

        if (nas_domain_list.Count < 1)
            throw new Exception($"Nas/Domain not found: (realm-id={realm_id})!");

        var ovpn_conf_file = Encoding.UTF8.GetString(cert_file);
        ovpn_conf_file = SetDomain(ovpn_conf_file, realm_name, nas_domain_list);
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

    public async Task UpdateTrafficData()
    {
        var last_update_times = await TrafficDataRepo.LastUpdateTime();

        if (last_update_times.Count <= 0) return;

        var loaded_traffic_task = SessionRadiusSrv.Value.GetTrafficData(last_update_times);

        var current_traffic_task = TrafficDataRepo.FetchOpen();

        var realms = await RealmRepo.GetByIds(last_update_times.Keys.ToList());

        var traffic_data_list = await Merge(
            await current_traffic_task, 
            await loaded_traffic_task,
            realms);

        var realm_changes = realms.Values.Where(r => r.HasChanged).ToList();

        if (realm_changes.Count > 0)
        {
            await RealmRepo.BachSave(realm_changes);
        }

        if (traffic_data_list.Count <= 0) return;

        await TrafficDataRepo.BachSave(traffic_data_list);
    }

    private async Task<Dictionary<RealmEntity, (double Rate, long Cap)>> LoadServersCapacity()
    {
        var index = DateTime.Now.AddDays(-30);

        var realms = await RealmRepo.FetchAllActiveRealm();

        var clusters = await ServerRepo.Value.GetAllActiveNasInRealm(realms.Select(r => r.Id));
        var server_ids = clusters.SelectMany(s => s.Value).Select(s => s.Id).ToList();

        var traffics = (await TrafficDataRepo.Fetch(server_ids, index))
            .ToDictionary(k =>
                k.Key, v =>
                v.Value.Select(t => (t.StartSession, t.TotalData))
                    .GroupBy(k => k.StartSession)
                    .Select(a => a.Select(x => x.TotalData).Sum())
                    .ToList());

        // TODO: Use IQR to find real average
        var real_traffics = traffics.ToDictionary(k => k.Key, v => v.Value.Average());

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
        var remotes = string.Join("\n", domains.Select(domain => $"remote {domain}"));
        cert = SetRemote()
            .Replace(cert, remotes);

        cert = SetTitle()
            .Replace(cert, $"setenv FRIENDLY_NAME \"{name}\"");

        return cert;
    }

    private async Task<List<TrafficDataEntity>> Merge(List<TrafficDataEntity> destination, List<TrafficDataBinding> source, Dictionary<int, RealmEntity> realms)
    {
        if (source.Count <= 0) return [];

        var now = DateTime.Now;

        var destination_dictionary = destination.GroupBy(k => k.NasId)
            .ToDictionary(k => k.Key, v => v.ToDictionary(x => x.SessionId));
        var new_data = new List<TrafficDataEntity>();

        var account_dictionary =
            await AccountRepo.Value.GetAccountIdByUsername(source.Select(traffic => traffic.Username).ToHashSet());

        var server_dictionary =
            await ServerRepo.Value.GetServerIdByIpAddress(
                source.Select(traffic => traffic.NasIpAddress)
                    .ToHashSet());

        foreach (var traffic in source)
        {
            if (!server_dictionary.TryGetValue(traffic.NasIpAddress, out var server))
            {
                Log.Error("The incoming traffic data had invalid nas-ip: ({0}).",
                    traffic.NasIpAddress);
                continue;
            }

            if (realms.TryGetValue(server.ReamId, out var realm))
            {
                realm.LastTrafficSync = now;
                realm.HasChanged = true;
            }

            if (destination_dictionary.TryGetValue(server.Id, out var data_pack) &&
                data_pack.TryGetValue(traffic.NasIpAddress, out var data))
            {
                if (data.DataIn == traffic.DataIn && data.DataOut == traffic.DataOut) continue;

                data.DataOut = traffic.DataOut;
                data.DataIn = traffic.DataIn;
                new_data.Add(data);
            }
            else
            {
                if (!account_dictionary.TryGetValue(traffic.Username, out var account_id))
                {
                    Log.Error("The incoming traffic data had invalid username: ({0}).",
                        traffic.Username);
                    continue;
                }

                new_data.Add(new TrafficDataEntity
                {
                    AccountId = account_id,
                    NasId = server.Id,
                    SessionId = traffic.SessionId,
                    DataIn = traffic.DataIn,
                    DataOut = traffic.DataOut,
                    StartSession = traffic.StartSession,
                    EndSession = traffic.EndSession,
                    Created = traffic.Created,
                });
            }
        }

        return new_data;
    }

    [GeneratedRegex(@"remote server\.domain\.name")]
    private static partial Regex SetRemote();

    [GeneratedRegex(@"setenv FRIENDLY_NAME ""[^""]+""")]
    private static partial Regex SetTitle();
}