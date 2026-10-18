using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using Serilog;

namespace PhotonBypass.Application.Plan;

class PlanUsageReader(
    Lazy<IPlanStateRepository> plan_state_repo,
    Lazy<IRenewalRepository> renewal_repo,
    Lazy<IServerRepository> server_repo) : IPlanUsageReader
{
    private Lazy<IPlanStateRepository> PlanStateRepo { get; } = plan_state_repo;
    private Lazy<IRenewalRepository> RenewalRepo { get; } = renewal_repo;
    private Lazy<IServerRepository> ServerRepo { get; } = server_repo;

    public async Task<PlanUsageSnapshot?> GetUsage(int account_id)
    {
        var plan = await PlanStateRepo.Value.GetPlanState(account_id);

        if (plan == null) return null;

        var renewal = await LoadXuiRenewal(plan);

        return BuildSnapshot(plan, renewal);
    }

    public async Task<Dictionary<int, PlanUsageSnapshot>> GetUsage(IEnumerable<int> account_ids)
    {
        var id_list = account_ids.Distinct().ToList();

        var plan_dictionary = (await PlanStateRepo.Value.GetAll())
            .Where(plan => id_list.Contains(plan.Id))
            .ToDictionary(plan => plan.Id);

        if (plan_dictionary.Count == 0) return [];

        var realm_servers = (await ServerRepo.Value.GetAllActiveRadius())
            .GroupBy(server => server.RealmId)
            .ToDictionary(group => group.Key, group => group.First());

        var renewal_dictionary = await RenewalRepo.Value.GetLatestByAccountIds(
            plan_dictionary.Values
                .Where(plan => IsXuiRealm(plan.RestrictedRealmId, realm_servers))
                .Select(plan => plan.Id));

        return plan_dictionary.Values.ToDictionary(
            plan => plan.Id,
            plan => BuildSnapshot(plan, renewal_dictionary.GetValueOrDefault(plan.Id)));
    }

    private async Task<RenewalEntity?> LoadXuiRenewal(PlanStateEntity plan)
    {
        if (plan.RestrictedRealmId is not { } realm_id)
        {
            return null;
        }

        var realm_servers = await ServerRepo.Value.GetActiveRadiusInRealmOrAll(realm_id);

        if (!realm_servers.Any(server => (server.Features & ServerFeature.Xui) != 0))
        {
            return null;
        }

        var renewal_dictionary = await RenewalRepo.Value.GetLatestByAccountIds([plan.Id]);

        return renewal_dictionary.GetValueOrDefault(plan.Id);
    }

    private static bool IsXuiRealm(int? realm_id, Dictionary<int, ServerEntity> realm_servers)
    {
        return realm_id.HasValue &&
               realm_servers.GetValueOrDefault(realm_id.Value) is { } server &&
               (server.Features & ServerFeature.Xui) != 0;
    }

    private static PlanUsageSnapshot BuildSnapshot(PlanStateEntity plan, RenewalEntity? renewal)
    {
        if (renewal is { LastPollTime: not null })
        {
            return new PlanUsageSnapshot
            {
                AccountId = plan.Id,
                TrafficUsed = renewal.TrafficUsed,
                // D16: TrafficLeft is the stacked remaining (CumulativeTrafficLeft
                // minus what the counter has consumed since the reset). A renewal
                // whose own limit is set while the stacked column is NULL predates
                // D16 and keeps reading the view instead.
                TrafficLeft = HasStackedTrafficLeft(renewal)
                    ? renewal.CumulativeTrafficLeft - renewal.TrafficUsed
                    : plan.TrafficLeft,
                LastActiveTime = renewal.LastActiveTime,
                ExpirationDate = HasStackedExpiry(renewal)
                    ? renewal.ExpirationDate
                    : plan.ExpirationDate,
            };
        }

        if (renewal != null)
        {
            Log.Debug("PlanUsageReader: x-ui renewal has never been polled; falling back to the view: " +
                      "(account-id={0}, renewal-id={1})", plan.Id, renewal.Id);
        }

        return FromView(plan);
    }

    private static bool HasStackedTrafficLeft(RenewalEntity renewal)
    {
        return renewal.CumulativeTrafficLeft.HasValue || renewal.TrafficLimit is null;
    }

    private static bool HasStackedExpiry(RenewalEntity renewal)
    {
        return renewal.ExpirationDate.HasValue || renewal.TimeLimitInDays is null;
    }

    private static PlanUsageSnapshot FromView(PlanStateEntity plan)
    {
        return new PlanUsageSnapshot
        {
            AccountId = plan.Id,
            TrafficUsed = plan.TrafficUsed,
            TrafficLeft = plan.TrafficLeft,
            LastActiveTime = plan.LastConnectTime,
            ExpirationDate = plan.ExpirationDate,
        };
    }
}
