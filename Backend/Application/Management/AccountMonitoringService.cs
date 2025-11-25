using PhotonBypass.Application.Authentication;
using PhotonBypass.Domain.Session;
using PhotonBypass.Domain.Session.Business;
using PhotonBypass.Domain.Session.Entity;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Session;
using PhotonBypass.Domain.Session.Business;
using PhotonBypass.Domain.Session.Entity;
using PhotonBypass.Domain.Session;
using Quartz;
using Serilog;

namespace PhotonBypass.Application.Management;

internal class AccountMonitoringService(
    IAccountStateRepository AccounyStateRepo,
    IAccountRepository AccountRepo,
    IHistoryRepository HistoryRepo,
    IAuthApplication AuthApp,
    IEmailService EmailSrv,
    IAccountRadiusSyncService RadiusSrv,
    IServerManagementService ServerMngSrv,
    ISocialMediaService SocialSrv)
    : IAccountMonitoringService, IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var plan_state_list = await AccounyStateRepo.GetAccountFinishingState();

        if (plan_state_list.Count < 1)
        {
            return;
        }

        await NotifSendServices(plan_state_list);

        Task.WaitAll(
            InactiveAbandonedUsers(plan_state_list),
            ServerMngSrv.CheckUserServerBalance());
    }

    public async Task InactiveAbandonedUsers(IEnumerable<AccountStateEntity> plan_state_list)
    {
        foreach (var plan in plan_state_list)
        {
            if (plan.ExpirationDate > DateTime.Now)
            {
                continue;
            }

            var account = await AccountRepo.GetAccount(plan.Id);

            if (account == null)
            {
                Log.Fatal(
                    "The account '{0}' is in 'PlanStateRepository.GetAccountFinishingState' but not found in 'AccountRepository'. account-id: {1}",
                    plan.Username, plan.Id);
                continue;
            }

            var expired_days = account.IsRichMaxDeactiveTime(plan.LastConnectTime);
            if (expired_days < 1)
            {
                continue;
            }

            if (account.ReferenceId.HasValue)
            {
                _ = RadiusSrv.ActiveUser(account.ReferenceId.Value, false);
            }

            Log.Information(
                "The user '{0}' was disabled: ExpiredTime={1} days, ExpirationDate={2}, TotalData={3}, DataUsage={4}",
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
    }

    public async Task NotifSendServices(IEnumerable<AccountStateEntity> plan_states)
    {
        var plan_state_list = plan_states.ToArray();
        var user_ids = plan_state_list.Select(x => x.Id).ToList();
        var accounts = await AccountRepo.GetAccounts(user_ids);

        var tasks = new List<Task>();

        foreach (var plan in plan_state_list)
        {
            if (!accounts.TryGetValue(plan.Id, out var account))
            {
                account = await AuthApp.CopyFromPermanentUser(plan.Username, null);
            }

            if (account == null || account.OverWarningTime())
            {
                continue;
            }

            var remains_title = plan.GetRemainsTitle();

            Log.Information("The user '{0}' is going to finish plan (x{1}, {2})",
                plan.Username, plan.SimultaneousUserCount, remains_title);

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

            if (account.Email != null)
            {
                tasks.Add(EmailSrv.FinishServiceAlert(
                    account.Fullname, plan.Username, account.Email, remains_title));

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