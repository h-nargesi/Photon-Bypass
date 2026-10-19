using PhotonBypass.Application.Plan;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Business;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Business;
using PhotonBypass.Domain.Plan.Entity;
using Quartz;
using Serilog;

namespace PhotonBypass.Application.Management;

internal class AccountMonitoringService(
    IAccountRepository account_repo,
    IHistoryRepository history_repo,
    Lazy<IPlanUsageReader> plan_usage_reader,
    Lazy<IEmailService> email_srv,
    Lazy<ISocialMediaService> social_srv,
    Lazy<IAccountRadiusSyncService> account_radius_srv)
    : IAccountMonitoringService
{
    /// <summary>Upper bound of concurrent warning sends (SMTP connections).</summary>
    private const int MaxConcurrentNotifications = 4;

    private IAccountRepository AccountRepo { get; } = account_repo;
    private IHistoryRepository HistoryRepo { get; } = history_repo;
    private Lazy<IPlanUsageReader> PlanUsageReader { get; } = plan_usage_reader;
    private Lazy<IEmailService> EmailSrv { get; } = email_srv;
    private Lazy<ISocialMediaService> SocialSrv { get; } = social_srv;
    private Lazy<IAccountRadiusSyncService> AccountRadiusSrv { get; } = account_radius_srv;

    public async Task InactiveAbandonedUsers(IEnumerable<PlanStateEntity> plan_state_list)
    {
        var now = DateTime.Now;

        var usage_dictionary = await PlanUsageReader.Value.GetUsage(plan_state_list.Select(plan => plan.Id));

        var abandoned_list = plan_state_list
            .Where(plan =>
            {
                var usage = usage_dictionary.GetValueOrDefault(plan.Id);
                var expiration = usage?.ExpirationDate ?? plan.ExpirationDate;
                var traffic_left = usage?.TrafficLeft ?? plan.TrafficLeft;

                return (expiration.HasValue && expiration.Value <= now) ||
                       (traffic_left.HasValue && traffic_left.Value < StaticValues.BytesInMegDouble);
            })
            .ToList();

        if (abandoned_list.Count < 1) return;

        var accounts = await AccountRepo.GetActiveAccounts(abandoned_list.Select(plan => plan.Id));

        var deactivate_list = new List<string>();
        var remove_list = new List<string>();
        var history_list = new List<HistoryEntity>();
        var remove_account_ids = new List<int>();

        foreach (var plan in abandoned_list)
        {
            if (!accounts.TryGetValue(plan.Id, out var account))
            {
                Log.Fatal(
                    "The account '{0}' is in 'SessionStateRepository.GetAccountFinishingState' but not found in 'AccountRepository'. account-id: {1}",
                    plan.Username, plan.Id);
                continue;
            }

            var last_activity = usage_dictionary.GetValueOrDefault(plan.Id)?.LastActiveTime ?? plan.LastConnectTime;

            var expired_days = account.IsReachedMaxInactivityDaysToDisable(last_activity);
            if (expired_days <= 0)
            {
                continue;
            }

            expired_days = account.IsReachedMaxInactivityDaysToDelete(last_activity);
            if (expired_days > 0)
            {
                Log.Information(
                    "The user '{0}' was deleted from radius servers: ExpiredTime={1} days, ExpirationDate={2}, TrafficLimit={3}, TrafficUsed={4}",
                    plan.Username, expired_days, plan.ExpirationDate, plan.TrafficLimit, plan.TrafficUsed);
                remove_list.Add(account.Username);

                remove_account_ids.Add(account.Id);
                continue;
            }

            deactivate_list.Add(account.Username);

            Log.Information(
                "The user '{0}' was disabled: ExpiredTime={1} days, ExpirationDate={2}, TrafficLimit={3}, TrafficUsed={4}",
                plan.Username, expired_days, plan.ExpirationDate, plan.TrafficLimit, plan.TrafficUsed);

            history_list.Add(new HistoryEntity
            {
                Target = plan.Id,
                Category = EventCategory.Security,
                Type = EventType.Warning,
                Title = "غیرفعال",
                Description =
                    "اکانت شما به علت عدم استفاده بعد از دو ماه غیرفعال شد. مقدار ترافیک یا مدت زمان باقیمانده به جای خود باقی است.",
                Value = DayHour((int)expired_days) + " گذشته",
            });
        }

        if (remove_account_ids.Count > 0)
        {
            await AccountRepo.SetDeactivated(remove_account_ids);
        }

        var task = HistoryRepo.Save(history_list);

        if (remove_list.Count > 0)
        {
            await AccountRadiusSrv.Value.RemoveUsers(remove_list);
        }

        if (deactivate_list.Count > 0)
        {
            await AccountRadiusSrv.Value.DeactivateUsers(deactivate_list);
        }

        await task;
    }

    public async Task NotifSendServices(IEnumerable<PlanStateEntity> plan_states)
    {
        var plan_state_list = plan_states.Where(plan => plan.IsFinishing()).ToArray();
        var account_ids = plan_state_list.Select(x => x.Id).ToList();
        var accounts = await AccountRepo.GetActiveAccounts(account_ids);

        // cap of concurrent SMTP connections: the SMTP server (and the send
        // timeout) must not see one connection per finishing account
        using var throttle = new SemaphoreSlim(MaxConcurrentNotifications);

        var dispatches = new List<(AccountEntity Account, HistoryEntity History, Task Send)>();

        foreach (var plan in plan_state_list)
        {
            if (!accounts.TryGetValue(plan.Id, out var account))
            {
                Log.Warning("The id ({0}) not found in accounts; skipped warning for this plan.", plan.Id);
                continue;
            }

            if (account.OverWarningTime())
            {
                continue;
            }

            var remains_title = plan.GetRemainsTitle();

            Log.Information("The user '{0}' is going to finish plan (x{1}, {2})",
                plan.Username, plan.SimultaneousUser, remains_title);

            if (!account.SendWarning)
            {
                continue;
            }

#if SOCIAL
            // WhatsApp-first: a validated mobile number gets the social alert
            // instead of the email below (the interface contract is
            // FinishServiceAlert(username, phone, type, left)).
            var phone = account.MobileNumber;

            if (phone != null)
            {
                dispatches.Add((account, WarnHistory(plan, remains_title), Dispatch(
                    () => SocialSrv.Value.FinishServiceAlert(
                        plan.Username, phone, plan.GetPlanTitle(), remains_title ?? string.Empty))));

                continue;
            }
#endif

            if (account.EmailAddress != null)
            {
                dispatches.Add((account, WarnHistory(plan, remains_title), Dispatch(
                    () => EmailSrv.Value.FinishServiceAlert(
                        account.Fullname, plan.Username, account.EmailAddress, plan.GetPlanTitle(), remains_title))));

                continue;
            }

            // stamp-after-success: an account without a reachable channel must
            // not be stamped either — the hourly rerun would skip it forever.
            // No history either: writing one for every finishing plan on every
            // hourly run spammed History for accounts that got no message.
            Log.Warning("No reachable channel for finishing-plan warning: account-id={0}, user={1}",
                account.Id, plan.Username);
        }

        try
        {
            await Task.WhenAll(dispatches.Select(dispatch => dispatch.Send));
        }
        catch
        {
            // per-dispatch failures are reported below; a failing send must
            // never fail the whole hourly JobInterval
        }

        var warned_account_ids = new List<int>();
        var history_list = new List<HistoryEntity>();

        foreach (var (account, history, send) in dispatches)
        {
            if (send.IsCompletedSuccessfully)
            {
                // stamp only after success: a failed account stays unstamped
                // so the next hourly run retries it naturally
                warned_account_ids.Add(account.Id);
                history_list.Add(history);
            }
            else if (send.IsFaulted)
            {
                Log.Warning(send.Exception, "Finish-service notification failed: account-id={0}, user={1}",
                    account.Id, account.Username);
            }
        }

        if (warned_account_ids.Count > 0)
        {
            await AccountRepo.UpdateWarningTimes(warned_account_ids);
        }

        if (history_list.Count > 0)
        {
            await HistoryRepo.Save(history_list);
        }

        async Task Dispatch(Func<Task> send)
        {
            await throttle.WaitAsync();

            try
            {
                await send();
            }
            finally
            {
                throttle.Release();
            }
        }
    }

    private static HistoryEntity WarnHistory(PlanStateEntity plan, string? remains_title)
    {
        // recorded only when a warning is actually dispatched: writing it for
        // every finishing plan on every hourly run spammed History for
        // accounts with no reachable channel (the throttle only stamps when a
        // message goes out)
        return new HistoryEntity
        {
            Target = plan.Id,
            Category = EventCategory.Renewal,
            Type = EventType.Warning,
            Title = "پایان پلن",
            Description = "اخطار پایان پلن.",
            Value = remains_title,
        };
    }

    private static string DayHour(double time)
    {
        if (time == 0) return "هیچ";
        var result = string.Empty;

        var day = (int)time;
        if (day > 0) result += $" و {day} روز";

        var hour = (int)((time - day) * 24);
        if (hour > 0) result += $" و {hour} ساعت";

        return result[3..];
    }
}