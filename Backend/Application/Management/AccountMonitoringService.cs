using Quartz;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Services;
using PhotonBypass.Application.Plan;
using Serilog;
using PhotonBypass.Domain.Account;
using PhotonBypass.Application.Authentication;

namespace PhotonBypass.Application.Management;

class AccountMonitoringService(
    ITopUpRepository TopUpRepo,
    IUserPlanStateRepository PlanStateRepo,
    IPermanentUsersRepository UserRepo,
    IAccountRepository AccountRepo,
    IHistoryRepository HistoryRepo,
    IAuthApplication AuthApp,
    IEmailService EmailSrv,
    ISocialMediaService SocialSrv)
    : IAccountMonitoringService, IJob
{
    public Task Execute(IJobExecutionContext context)
    {
        return Task.WhenAll(
            DeactiveAbandonedUsers(),
            NotifSendServices());
    }

    public async Task DeactiveAbandonedUsers()
    {
        var planStateList = await PlanStateRepo.GetPlanOverState(0.01F);

        throw new NotImplementedException();
    }

    public async Task NotifSendServices()
    {
        var planStateList = await PlanStateRepo.GetPlanOverState(0.1F);

        if (planStateList.Count < 1)
        {
            return;
        }

        var userIds = planStateList.Select(x => x.Id).ToList();
        var contacts_task = UserRepo.GetUsersContactInfo(userIds);
        var aqccounts_tak = AccountRepo.GetAccounts(userIds);

        var contacts = await contacts_task;
        var accounts = await aqccounts_tak;

        var tasks = new List<Task>();

        foreach (var plan in planStateList)
        {
            var remainsTitle = plan.GetRemainsTitle();

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

            if (!contacts.TryGetValue(plan.Id, out var contact))
            {
                continue;
            }

            if (!accounts.TryGetValue(plan.Id, out var account))
            {
                account = await AuthApp.CopyFromPermanentUser(plan.Username, null);
            }

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
        account.WarningTimes += 1;
        _ = AccountRepo.Save(account);
    }
}
