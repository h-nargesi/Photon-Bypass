using Quartz;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Services;
using PhotonBypass.Application.Plan;
using Serilog;
using PhotonBypass.Domain.Account;
using PhotonBypass.Application.Authentication;
using PhotonBypass.Domain.Radius;

namespace PhotonBypass.Application.Management;

class AccountMonitoringService(
    IUserPlanStateRepository PlanStateRepo,
    IPermanentUsersRepository UserRepo,
    IAccountRepository AccountRepo,
    IHistoryRepository HistoryRepo,
    IAuthApplication AuthApp,
    IEmailService EmailSrv,
    IRadiusService RadiusSrv,
    IServerManagementService ServerMngSrv,
    ISocialMediaService SocialSrv)
    : IAccountMonitoringService, IJob
{
    private const int MAX_DEACTIVATE_PLAN = 60;
    private const int DELAY_BETWEEN_WARNINGS = 20;

    public async Task Execute(IJobExecutionContext context)
    {
        var planStateList = await PlanStateRepo.GetPlanOverState(0.1F);

        if (planStateList.Count < 1)
        {
            return;
        }

        await NotifSendServices(planStateList);

        Task.WaitAll(
            DeactiveAbandonedUsers(planStateList),
            ServerMngSrv.CheckUserServerBalance());
    }

    public async Task DeactiveAbandonedUsers(IEnumerable<UserPlanStateEntity> planStateList)
    {
        foreach (var plan in planStateList)
        {
            var expired_time = 0D;
            if (plan.PlanType == PlanType.Monthly)
            {
                if (plan.ExpirationDate.HasValue)
                {
                    expired_time = (plan.ExpirationDate.Value - DateTime.Now).TotalDays;
                    if (expired_time < MAX_DEACTIVATE_PLAN)
                    {
                        continue;
                    }
                }
                else
                {
                    Log.Fatal("The user '{0}' is monthly but does not have expiration date. user-id: {1}", plan.Username, plan.Id);
                }
            }
            else if (plan.PlanType == PlanType.Traffic)
            {
                if (plan.TotalData.HasValue)
                {
                    var user = await UserRepo.GetUser(plan.Id);

                    if (user == null)
                    {
                        Log.Fatal("The user '{0}' is in 'ph_v_users_balance' but not found in 'permanent_users'. user-id: {1}", plan.Username, plan.Id);
                    }
                    else
                    {
                        expired_time = ((user.LastAcceptTime ?? user.CreatedTime) - DateTime.Now).TotalDays;
                        if (expired_time < MAX_DEACTIVATE_PLAN)
                        {
                            continue;
                        }
                    }
                }
                else
                {
                    Log.Fatal("The user '{0}' is traffic but does not have data limitation. user-id: {1}", plan.Username, plan.Id);
                }
            }

            _ = RadiusSrv.ActivePermanentUser(plan.Id, false);

            Log.Information("The user '{0}' was disabled: ExpiredTime={1} days, ExpirationDate={2}, TotalData={3}, DataUsage={4}", 
                plan.Username, expired_time, plan.ExpirationDate, plan.TotalData, plan.DataUsage);

            _ = HistoryRepo.Save(new HistoryEntity
            {
                Issuer = "ادمین",
                Target = plan.Username,
                EventTime = DateTime.Now,
                Title = "غیرفعال",
                Description = "اکانت شما به علت عدم استفاده بعد از دو ماه غیرفعال شد. مقدار ترافیک یا مدت زمان باقیمانده به جای خود باقی است.",
                Unit = "روز گذشته",
                Value = (int)expired_time,
            });
        }
    }

    public async Task NotifSendServices(IEnumerable<UserPlanStateEntity> planStateList)
    {
        var userIds = planStateList.Select(x => x.Id).ToList();
        var contacts_task = UserRepo.GetUsersContactInfo(userIds);
        var aqccounts_tak = AccountRepo.GetAccounts(userIds);

        var contacts = await contacts_task;
        var accounts = await aqccounts_tak;

        var tasks = new List<Task>();

        foreach (var plan in planStateList)
        {
            if (!accounts.TryGetValue(plan.Id, out var account))
            {
                account = await AuthApp.CopyFromPermanentUser(plan.Username, null);
            }
            else if (account.WarningTimes.HasValue &&
                (account.WarningTimes.Value - DateTime.Now).TotalHours <= DELAY_BETWEEN_WARNINGS)
            {
                continue;
            }

            Log.Information("The user '{0}' is going to finish plan ({1}, {2}, x{3}, {4})",
                plan.Username, plan.PlanType.ToString(), plan.SimultaneousUserCount, plan.GetRemainsTitle());

            _ = HistoryRepo.Save(new HistoryEntity
            {
                Issuer = "ادمین",
                Target = plan.Username,
                EventTime = DateTime.Now,
                Title = "پایان پلن",
                Description = "اخطار پایان پلن.",
                Unit = plan.PlanType == PlanType.Monthly ? "ماهانه" : "ترافیک",
                Value = plan.PlanType == PlanType.Monthly ? plan.LeftDays : plan.GigaLeft.ToString(),
            });

            if (account?.SendWarning != true)
            {
                continue;
            }

            if (!contacts.TryGetValue(plan.Id, out var contact))
            {
                continue;
            }

            var remainsTitle = plan.GetRemainsTitle();

            if (contact.Phone != null)
            {
                tasks.Add(SocialSrv.FinishServiceAlert(plan.Username, contact.Phone, plan.PlanType, remainsTitle));

                if (account != null)
                {
                    IncreaseWarningTimes(account);
                }

                continue;
            }

            if (contact.Email != null)
            {
                tasks.Add(EmailSrv.FinishServiceAlert(plan.Username, contact.Email, plan.PlanType, remainsTitle));

                if (account != null)
                {
                    IncreaseWarningTimes(account);
                }

                continue;
            }
        }

        await Task.WhenAll(tasks);
    }

    private void IncreaseWarningTimes(AccountEntity account)
    {
        account.WarningTimes = DateTime.Now;
        _ = AccountRepo.Save(account);
    }
}
