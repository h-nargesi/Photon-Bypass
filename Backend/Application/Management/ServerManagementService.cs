using Microsoft.Extensions.Options;
using PhotonBypass.Application.Management.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Management.Model;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Business;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using Serilog;
using System.Text;
using System.Text.RegularExpressions;
using OperatingSystem = PhotonBypass.Domain.Servers.Types.OperatingSystem;

namespace PhotonBypass.Application.Management;

partial class ServerManagementService(
    IRealmRepository realm_repo,
    ITrafficDataRepository traffic_data_repo,
    Lazy<IPlanStateRepository> plan_state_repo,
    Lazy<ISessionRadiusSyncService> session_radius_srv,
    Lazy<IServerRepository> server_repo,
    Lazy<IAccountRepository> account_repo,
    Lazy<ISocialMediaService> social_srv,
    Lazy<IJobContext> job_context,
    IOptions<ManagementOptions> options)
    : IServerManagementService
{
    private const int CapacityWindowDays = 30;

    private const double BytesPerMegabitPerSecond = 125_000;

    private const double CapacityWindowSeconds = CapacityWindowDays * 24 * 60 * 60;

    private IRealmRepository RealmRepo { get; } = realm_repo;
    private ITrafficDataRepository TrafficDataRepo { get; } = traffic_data_repo;
    private Lazy<IPlanStateRepository> PlanStateRepo { get; } = plan_state_repo;
    private Lazy<ISessionRadiusSyncService> SessionRadiusSrv { get; } = session_radius_srv;
    private Lazy<IServerRepository> ServerRepo { get; } = server_repo;
    private Lazy<IAccountRepository> AccountRepo { get; } = account_repo;
    private Lazy<ISocialMediaService> SocialSrv { get; } = social_srv;
    private Lazy<IJobContext> JobContext { get; } = job_context;
    private IOptions<ManagementOptions> Options { get; } = options;

    public async Task<RealmEntity> GetAvailableRealm()
    {
        var server_capacities = await LoadRealmsCapacityState();

        var realm = server_capacities
            .Where(realm => realm.DemandRate > -1)
            .OrderBy(s => s.DemandRate)
            .Select(s => s.Realm)
            .FirstOrDefault();

        return realm ?? throw new UserException("ظرفیت سرورها تکمیل است؛ لطفاً بعداً تلاش کنید!",
            "No realm with free capacity is available.");
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
        var realms = await LoadRealmsCapacityState();

        var alarms = new List<string>();

        foreach (var realm_state in realms.Where(r => r.UsageRate > -1))
        {
            var percent = 100 * realm_state.UsageRate;

            switch (percent)
            {
                case < 10:
                    alarms.Add($"Unused Realm: {realm_state.Realm.Name} ({percent:N2}% from {realm_state.EffectiveCapacityBytes})");
                    break;
                case > 90:
                    alarms.Add($"Low Capacity: {realm_state.Realm.Name} ({percent:N2}% from {realm_state.EffectiveCapacityBytes})");
                    break;
            }
        }

        if (alarms.Count != 0)
        {
            await SocialSrv.Value.AlarmServerCapacity(alarms);
        }
    }

    public async Task<DataPlaneInfoModel?> GetDataPlaneInfo(int? realm_id)
    {
        var radius_list = await ServerRepo.Value.GetActiveRadiusInRealmOrAll(realm_id);
        var nas_list = await ServerRepo.Value.GetActiveNasInRealmOrAll(realm_id);

        var kind = MergeDataPlaneKind(radius_list);

        if (realm_id.HasValue)
        {
            return new DataPlaneInfoModel
            {
                Kind = kind,
                SupportsOvpnCert = nas_list.Any(IsOvpnCertSource),
                SupportsPasswordChange = radius_list.Count > 0 && kind != DataPlaneKind.Xui,
            };
        }

        var all_radius = await ServerRepo.Value.GetAllActiveRadius();

        return new DataPlaneInfoModel
        {
            Kind = kind,
            SupportsOvpnCert = nas_list.Any(IsOvpnCertSource),
            SupportsPasswordChange = all_radius.All(server => GetDataPlaneKind(server) != DataPlaneKind.Xui),
        };
    }

    public async Task<ApiResult> SetRealmCapacityOverride(int realm_id, long? override_bytes)
    {
        if (override_bytes is < 0)
        {
            throw new UserException("مقدار ظرفیت واردشده معتبر نیست!",
                $"Capacity override must be null or non-negative: {override_bytes}");
        }

        var realm = (await RealmRepo.GetByIds([realm_id])).TryGetValue(realm_id, out var found) ? found : null;

        if (realm is not { IsActive: true })
        {
            throw new UserException("سرور مورد نظر پیدا نشد!", $"Active realm not found: {realm_id}");
        }

        var old_value = realm.CapacityOverrideBytes;

        var updated = await RealmRepo.SetCapacityOverride(realm_id, override_bytes);

        if (!updated)
        {
            throw new UserException("سرور مورد نظر پیدا نشد!", $"Realm capacity update missed: {realm_id}");
        }

        Log.Information("[user: {0}] Realm capacity override changed: (realm:{1}, {2} -> {3})",
            JobContext.Value.Username, realm_id, old_value, override_bytes);

        return ApiResult.Success("ظرفیت سرور به‌روزرسانی شد.");
    }

    public async Task<List<RealmCapacityModel>> GetRealmsCapacityState()
    {
        var states = await LoadRealmsCapacityState();

        return states.Select(state => new RealmCapacityModel
            {
                RealmId = state.Realm.Id,
                Name = state.Realm.Name,
                ComputedCapacityBytes = state.ComputedCapacityBytes,
                OverrideBytes = state.OverrideBytes,
                EffectiveCapacityBytes = state.EffectiveCapacityBytes,
                DemandBytes = state.DemandBytes,
                ObservedUsageBytes = state.ObservedUsageBytes,
                Rate = state.DemandRate,
                IsEligibleForAssignment = state.IsEligibleForAssignment,
            })
            .ToList();
    }

    public async Task UpdateTrafficData()
    {
        var last_update_times = await TrafficDataRepo.LastUpdateTime();

        if (last_update_times.Count <= 0) return;

        var locked_realms = (await RealmRepo.TryLockTrafficSync(last_update_times.Keys,
            RenewalBusiness.UpdateTrafficDataTimeSecondLimit)).ToHashSet();

        if (locked_realms.Count <= 0) return;

        var sync_times = last_update_times.Where(pair => locked_realms.Contains(pair.Key))
            .ToDictionary();

        // Load the radius-server list before the parallel section: repositories in
        // this scope share one SqlConnection, so the database reads must all finish
        // before the router fetch and the local traffic fetch start running in
        // parallel. The short-lived cache (ServerRepository) keeps this cheap.
        var radius_list = await ServerRepo.Value.GetAllActiveRadius();

        var from = sync_times.Values.Where(time => time.HasValue)
            .Select(time => time!.Value)
            .DefaultIfEmpty(DateTime.MinValue)
            .Min();

        if (from > DateTime.MinValue) from = from.AddSeconds(-1);

        var loaded_traffic_task = SessionRadiusSrv.Value.GetTrafficData(radius_list, sync_times);
        var current_traffic_task = TrafficDataRepo.Fetch(from);

        var loaded_traffic = await loaded_traffic_task;
        var current_traffic = await current_traffic_task;

        var traffic_data_list = await Merge(current_traffic, loaded_traffic);

        if (traffic_data_list.Count <= 0) return;

        await TrafficDataRepo.Save(traffic_data_list);
    }

    private async Task<List<RealmCapacityState>> LoadRealmsCapacityState()
    {
        var realms = (await RealmRepo.FetchAllActiveRealm())
            .ToDictionary(k => k.Id);

        var clusters = await ServerRepo.Value.GetAllActiveNasInRealm(realms.Keys);

        var demand_by_realm = await LoadRealmDemand(realms.Keys);
        var usage_by_nas = await LoadObservedUsage(clusters.SelectMany(cluster => cluster.Value)
            .Select(server => server.Id));

        return clusters.Select(cluster =>
            {
                var realm = realms[cluster.Key];

                return new RealmCapacityState
                {
                    Realm = realm,
                    ComputedCapacityBytes = cluster.Value.Sum(server => server.BandWidth),
                    DemandBytes = demand_by_realm.GetValueOrDefault(cluster.Key),
                    ObservedUsageBytes = cluster.Value.Sum(server => usage_by_nas.GetValueOrDefault(server.Id)),
                };
            })
            .ToList();
    }

    private async Task<Dictionary<int, double>> LoadRealmDemand(IEnumerable<int> active_realm_ids)
    {
        var now = DateTime.Now;
        var realm_mask = active_realm_ids.ToHashSet();

        var active_plans = (await PlanStateRepo.Value.GetAll())
            .Where(plan => plan.IsActive &&
                           (plan.ExpirationDate == null || plan.ExpirationDate > now) &&
                           (plan.TrafficLeft == null || plan.TrafficLeft > 0))
            .ToList();

        var demand_by_realm = new Dictionary<int, double>();

        foreach (var plan in active_plans)
        {
            var plan_demand = GetPlanDemand(plan);

            if (plan.RestrictedRealmId is int realm_id)
            {
                if (realm_mask.Contains(realm_id))
                {
                    demand_by_realm[realm_id] = demand_by_realm.GetValueOrDefault(realm_id) + plan_demand;
                }

                continue;
            }

            foreach (var realm in realm_mask)
            {
                demand_by_realm[realm] = demand_by_realm.GetValueOrDefault(realm) + plan_demand;
            }
        }

        return demand_by_realm;
    }

    private double GetPlanDemand(PlanStateEntity plan)
    {
        double? quota_bound = plan.TrafficLimit.HasValue && plan.TimeLimitInDays.HasValue
            ? plan.TrafficLimit.Value * (CapacityWindowDays / (double)Math.Max((int)plan.TimeLimitInDays.Value, 1))
            : null;

        double? rate_bound = plan.RateLimitInMeg.HasValue
            ? (double)plan.RateLimitInMeg.Value * BytesPerMegabitPerSecond * CapacityWindowSeconds *
              Math.Max((int)plan.SimultaneousUser, 1)
            : null;

        return (quota_bound, rate_bound) switch
        {
            ({ } quota, { } rate) => Math.Min(quota, rate),
            ({ } quota, null) => quota,
            (null, { } rate) => rate,
            (null, null) => Options.Value.UnmeteredPlanDemandGigabytes * StaticValues.BytesInGigDouble,
        };
    }

    private async Task<Dictionary<int, double>> LoadObservedUsage(IEnumerable<int> nas_ids)
    {
        var index = DateTime.Now.AddDays(-CapacityWindowDays);

        var traffics = await TrafficDataRepo.Fetch(nas_ids, index);

        return traffics.ToDictionary(
            k => k.Key,
            v => v.Value.Sum(traffic => (double)traffic.TotalData));
    }

    private static string? MergeDataPlaneKind(IEnumerable<ServerEntity> radius_servers)
    {
        var kinds = radius_servers.Select(GetDataPlaneKind).Distinct().ToList();

        return kinds.Count == 1 ? kinds[0] : null;
    }

    private static string? GetDataPlaneKind(ServerEntity server)
    {
        return (server.Features & ServerFeature.Radius) switch
        {
            ServerFeature.UserManager => DataPlaneKind.UserManager,
            ServerFeature.RadiusDesk => DataPlaneKind.RadiusDesk,
            ServerFeature.Xui => DataPlaneKind.Xui,
            _ => null,
        };
    }

    private static bool IsOvpnCertSource(ServerEntity nas)
    {
        return (nas.Features & ServerFeature.Ovpn) != 0 && nas.OsType == OperatingSystem.Mikrotik;
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

    private async Task<List<TrafficDataEntity>> Merge(List<TrafficDataEntity> destination, List<TrafficDataBinding> source)
    {
        if (source.Count <= 0) return [];

        var destination_dictionary = destination.GroupBy(k => k.NasId)
            .ToDictionary(k => k.Key, v => v.GroupBy(x => x.SessionId)
                .ToDictionary(g => g.Key, g => g.MaxBy(x => x.Id)!));
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

            if (destination_dictionary.TryGetValue(server.Id, out var data_pack) &&
                data_pack.TryGetValue(traffic.SessionId, out var data))
            {
                if (data.DataIn == traffic.DataIn && data.DataOut == traffic.DataOut &&
                    data.EndSession == traffic.EndSession) continue;

                data.DataOut = traffic.DataOut;
                data.DataIn = traffic.DataIn;
                data.EndSession = traffic.EndSession;
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

    private sealed record RealmCapacityState
    {
        public required RealmEntity Realm { get; init; }

        public long ComputedCapacityBytes { get; init; }

        public double DemandBytes { get; init; }

        public double ObservedUsageBytes { get; init; }

        public long? OverrideBytes => Realm.CapacityOverrideBytes;

        public long EffectiveCapacityBytes => OverrideBytes ?? ComputedCapacityBytes;

        public bool IsEligibleForAssignment => EffectiveCapacityBytes > 0;

        public double DemandRate => IsEligibleForAssignment ? DemandBytes / EffectiveCapacityBytes : -1;

        public double UsageRate => IsEligibleForAssignment ? ObservedUsageBytes / EffectiveCapacityBytes : -1;
    }
}
