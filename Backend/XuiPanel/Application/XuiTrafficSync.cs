using Microsoft.Extensions.Options;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Infra.Radius;
using PhotonBypass.Xui.WebService;
using Serilog;

namespace PhotonBypass.Xui.Application;

public interface IXuiTrafficSync
{
    Task Run();
}

public class XuiTrafficSync(
    Lazy<IXuiApi> api,
    Lazy<IServerRepository> server_repo,
    Lazy<IAccountRepository> account_repo,
    Lazy<IRenewalRepository> renewal_repo,
    Lazy<ITrafficDataRepository> traffic_data_repo,
    Lazy<IRealmRepository> realm_repo,
    IOptions<XuiTrafficOptions> options) : IXuiTrafficSync
{
    private const int HistoryWindowDays = 30;

    private Lazy<IXuiApi> Api { get; } = api;
    private Lazy<IServerRepository> ServerRepo { get; } = server_repo;
    private Lazy<IAccountRepository> AccountRepo { get; } = account_repo;
    private Lazy<IRenewalRepository> RenewalRepo { get; } = renewal_repo;
    private Lazy<ITrafficDataRepository> TrafficDataRepo { get; } = traffic_data_repo;
    private Lazy<IRealmRepository> RealmRepo { get; } = realm_repo;
    private IOptions<XuiTrafficOptions> Options { get; } = options;

    public async Task Run()
    {
        var server_list = (await ServerRepo.Value.GetAllActiveRadius())
            .Where(server => server.GetSyncKey() == RadiusType.Xui)
            .GroupBy(server => server.RealmId)
            .Select(group => group.First())
            .ToList();

        if (server_list.Count == 0) return;

        var locked_realms = (await RealmRepo.Value.TryLockTrafficSync(
            server_list.Select(server => server.RealmId), Options.Value.LockThresholdSeconds)).ToHashSet();

        foreach (var server in server_list.Where(server => locked_realms.Contains(server.RealmId)))
        {
            try
            {
                await Collect(server);
            }
            catch (Exception error)
            {
                Log.Error(error, "XuiTrafficCollector failed: (realm-id={0}, server-id={1}, server={2})",
                    server.RealmId, server.Id, server.Name);
            }
        }
    }

    private async Task Collect(ServerEntity server)
    {
        var now = DateTime.Now;
        var stats_list = await Api.Value.ListClientTraffic(server);

        var known_accounts = await AccountRepo.Value.GetAccountIdByUsername(
            stats_list.Select(stats => stats.Email).ToHashSet());

        foreach (var unknown in stats_list.Where(stats => !known_accounts.ContainsKey(stats.Email)))
        {
            Log.Warning("XuiTrafficCollector: unknown x-ui client skipped (reconciliation signal): " +
                        "(server={0}, email={1})", server.Name, unknown.Email);
        }

        if (known_accounts.Count == 0) return;

        var renewal_dictionary = await RenewalRepo.Value.GetLatestByAccountIds(known_accounts.Values);

        var stats_by_account = stats_list
            .Where(stats => known_accounts.TryGetValue(stats.Email, out var account_id) &&
                            renewal_dictionary.ContainsKey(account_id))
            .GroupBy(stats => known_accounts[stats.Email])
            .ToDictionary(group => group.Key, group => group.First());

        if (stats_by_account.Count == 0) return;

        var today_session_ids = stats_by_account.Keys
            .Select(account_id => XuiTrafficRow.SessionId(account_id, renewal_dictionary[account_id].Id, now))
            .ToList();

        var today_rows = (await TrafficDataRepo.Value.Fetch(server.Id, today_session_ids))
            .ToDictionary(row => row.SessionId);

        var last_nas_ids = await TrafficDataRepo.Value.GetLastNasId(known_accounts.Values, XuiTrafficRow.SessionIdPrefix);

        await RenewalRepo.Value.DbContext.BeginTransactionAsync();

        try
        {
            var rows = new List<TrafficDataEntity>();
            var changed_renewals = new List<RenewalEntity>();

            foreach (var pair in stats_by_account)
            {
                var renewal = renewal_dictionary[pair.Key];

                var result = XuiTrafficAccounting.Compute(
                    server.Id, now, pair.Value, renewal,
                    today_rows.GetValueOrDefault(XuiTrafficRow.SessionId(pair.Key, renewal.Id, now)),
                    last_nas_ids.TryGetValue(pair.Key, out var last_nas_id) ? last_nas_id : null,
                    await LoadDailyHistory(pair.Key, renewal.LastPollTime, now));

                if (result.EpochAlarm)
                {
                    Log.Error("XUI-EPOCH-ALARM: the x-ui counter dropped below the anchor " +
                              "(manual reset/restore suspected): (server={0}, account-id={1}, renewal-id={2}, " +
                              "anchor={3}, current={4}, charged={5})",
                        server.Name, pair.Key, renewal.Id, renewal.LastSeenCounter,
                        result.NewSeenCounter, result.Delta);
                }

                if (result.ServerChange)
                {
                    Log.Warning("XuiTrafficCollector: the realm's x-ui server changed; anchor re-based, " +
                                "delta skipped until cleanup: (realm-id={0}, account-id={1})",
                                server.RealmId, pair.Key);
                }

                renewal.LastSeenCounter = result.NewSeenCounter;
                renewal.LastPollTime = now;
                renewal.TrafficUsed = result.NewTrafficUsed;

                if (result.NewLastActiveTime is { } last_active)
                {
                    renewal.LastActiveTime = last_active;
                }

                changed_renewals.Add(renewal);
                rows.AddRange(result.Rows);
            }

            await TrafficDataRepo.Value.Save(rows);
            await RenewalRepo.Value.Save(changed_renewals);

            await RenewalRepo.Value.DbContext.CommitAsync();
        }
        catch
        {
            try
            {
                await RenewalRepo.Value.DbContext.RollbackAsync();
            }
            catch (Exception rollback_error)
            {
                Log.Error(rollback_error, "XuiTrafficCollector rollback failed: (server={0})", server.Name);
            }

            throw;
        }
    }

    private async Task<IReadOnlyDictionary<DateTime, long>?> LoadDailyHistory(
        int account_id, DateTime? last_poll, DateTime now)
    {
        if (last_poll is not { } poll || poll.Date >= now.Date)
        {
            return null;
        }

        var history = await TrafficDataRepo.Value.Fetch(account_id, now.AddDays(-HistoryWindowDays));

        return history
            .GroupBy(row => row.StartSession.Date)
            .ToDictionary(group => group.Key, group => group.Sum(row => row.TotalData));
    }
}
