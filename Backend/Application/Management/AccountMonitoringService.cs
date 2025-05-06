using Quartz;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Services;
using PhotonBypass.Application.Plan;
using Serilog;
using PhotonBypass.Domain.Account;

namespace PhotonBypass.Application.Management;

class AccountMonitoringService(
    ITopUpRepository TopUpRepo,
    IUserPlanStateRepository PlanStateRepo,
    IPermanentUsersRepository UserRepo,
    IAccountRepository AccountRepo,
    IHistoryRepository HistoryRepo,
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

    public Task DeactiveAbandonedUsers()
    {
        throw new NotImplementedException();
    }

    public async Task NotifSendServices()
    {
        var planStateList = await PlanStateRepo.GetPlanOverState(0.1F);

        var userIds = planStateList.Select(x => x.Id).ToList();

        var contacts = await UserRepo.GetUsersContactInfo(userIds);

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

            if (contact.Phone != null)
            {
                tasks.Add(SocialSrv.FinishServiceAlert(plan.Username, contact.Phone, plan.PlanType, remainsTitle));
                continue;
            }

            if (contact.Email != null)
            {
                tasks.Add(EmailSrv.FinishServiceAlert(plan.Username, contact.Email, plan.PlanType, remainsTitle));
                continue;
            }
        }

        await Task.WhenAll(tasks);
    }
}
