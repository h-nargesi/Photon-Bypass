using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Business;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Business;
using PhotonBypass.Domain.Plan.Entity;
using Quartz;
using Serilog;

namespace PhotonBypass.Application.Management;

internal class AccountMonitoringService(
    IPlanStateRepository plan_state_repo,
    IAccountRepository account_repo,
    IHistoryRepository history_repo,
    Lazy<IEmailService> email_srv,
    Lazy<IAccountRadiusSyncService> account_radius_srv,
    Lazy<IServerManagementService> server_mng_srv)
    : IAccountMonitoringService, IJob
{
    private IPlanStateRepository PlanStateRepo { get; } = plan_state_repo;
    private IAccountRepository AccountRepo { get; } = account_repo;
    private IHistoryRepository HistoryRepo { get; } = history_repo;
    private Lazy<IEmailService> EmailSrv { get; } = email_srv;
    private Lazy<IAccountRadiusSyncService> AccountRadiusSrv { get; } = account_radius_srv;
    private Lazy<IServerManagementService> ServerMngSrv { get; } = server_mng_srv;
    
    public async Task Execute(IJobExecutionContext context)
    {
        var plan_state_list = await PlanStateRepo.GetAll();

        if (plan_state_list.Count < 1)
        {
            return;
        }
        
        var finishing_list = plan_state_list.Where(plan => plan.IsFinishing()).ToList();

        await NotifSendServices(finishing_list);

        Task.WaitAll(
            AccountRadiusSrv.Value.DeactivateInvalidRadiusUsers(plan_state_list),
            InactiveAbandonedUsers(finishing_list),
            ServerMngSrv.Value.CheckUserServerBalance());
    }

    public async Task InactiveAbandonedUsers(IEnumerable<PlanStateEntity> plan_state_list)
    {
        var deactivate_list = new List<string>();
        var remove_list = new List<string>();

        foreach (var plan in plan_state_list)
        {
            if ((!plan.ExpirationDate.HasValue || plan.ExpirationDate > DateTime.Now) && 
                (!plan.TrafficLeft.HasValue || plan.TrafficLeft >= StaticValues.BytesInMegDouble))
            {
                continue;
            }

            var account = await AccountRepo.GetAccount(plan.Id);

            if (account == null)
            {
                Log.Fatal(
                    "The account '{0}' is in 'SessionStateRepository.GetAccountFinishingState' but not found in 'AccountRepository'. account-id: {1}",
                    plan.Username, plan.Id);
                continue;
            }

            var expired_days = account.IsReachedMaxInactivityDaysToDisable(plan.LastConnectTime);
            if (expired_days <= 0)
            {
                continue;
            }
            
            expired_days = account.IsReachedMaxInactivityDaysToDelete(plan.LastConnectTime);
            if (expired_days > 0)
            {
                Log.Information(
                    "The user '{0}' was deleted from radius servers: ExpiredTime={1} days, ExpirationDate={2}, TrafficLimit={3}, TrafficUsed={4}",
                    plan.Username, expired_days, plan.ExpirationDate, plan.TrafficLimit, plan.TrafficUsed);
                remove_list.Add(account.Username);
                
                account.Active = false;
                _ = AccountRepo.Save(account);
                continue;
            }

            deactivate_list.Add(account.Username);

            Log.Information(
                "The user '{0}' was disabled: ExpiredTime={1} days, ExpirationDate={2}, TrafficLimit={3}, TrafficUsed={4}",
                plan.Username, expired_days, plan.ExpirationDate, plan.TrafficLimit, plan.TrafficUsed);

            _ = HistoryRepo.Save(new HistoryEntity
            {
                Issuer = "ادمین",
                Target = plan.Username,
                EventTime = DateTime.Now,
                Title = "غیرفعال",
                Description =
                    "اکانت شما به علت عدم استفاده بعد از دو ماه غیرفعال شد. مقدار ترافیک یا مدت زمان باقیمانده به جای خود باقی است.",
                Unit = "روز گذشته",
                Value = expired_days,
            });
        }

        if (remove_list.Count > 0)
        {
            await AccountRadiusSrv.Value.RemoveUsers(remove_list);
        }

        if (deactivate_list.Count > 0)
        {
            await AccountRadiusSrv.Value.DeactivateUsers(deactivate_list);
        }
    }

    public async Task NotifSendServices(IEnumerable<PlanStateEntity> plan_states)
    {
        var plan_state_list = plan_states.ToArray();
        var account_ids = plan_state_list.Select(x => x.Id).ToList();
        var accounts = await AccountRepo.GetAccounts(account_ids);

        var tasks = new List<Task>();

        foreach (var plan in plan_state_list)
        {
            if (!accounts.TryGetValue(plan.Id, out var account))
            {
                throw new Exception($"The id ({plan.Id}) not found in accounts.");
            }

            if (account.OverWarningTime())
            {
                continue;
            }

            var remains_title = plan.GetRemainsTitle();

            Log.Information("The user '{0}' is going to finish plan (x{1}, {2})",
                plan.Username, plan.SimultaneousUser, remains_title);

            _ = HistoryRepo.Save(new HistoryEntity
            {
                Issuer = "ادمین",
                Target = plan.Username,
                EventTime = DateTime.Now,
                Title = "پایان پلن",
                Description = "اخطار پایان پلن.",
                Value = remains_title,
            });

            if (!account.SendWarning)
            {
                continue;
            }

#if SOCIAL
            if (contact.Phone != null)
            {
                tasks.Add(SocialSrv.FinishServiceAlert(plan.Username, contact.Phone, plan.PlanType, remainsTitle));

                if (account != null)
                {
                    IncreaseWarningTime(account);
                }

                continue;
        }
#endif

            if (account.EmailAddress != null)
            {
                tasks.Add(EmailSrv.Value.FinishServiceAlert(
                    account.Fullname, plan.Username, account.EmailAddress, plan.GetPlanTitle(), remains_title));

                IncreaseWarningTime(account);
            }
        }

        await Task.WhenAll(tasks);
    }

    private void IncreaseWarningTime(AccountEntity account)
    {
        account.UpdateWarningTime();
        _ = AccountRepo.Save(account);
    }
}