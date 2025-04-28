using Quartz;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Services;

namespace PhotonBypass.Application.Management;

class AccountMonitoringService(
    ITopUpRepository TopUpRepo,
    IUserPlanStateRepository PlanStateRepo,
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

    public Task NotifSendServices()
    {
        throw new NotImplementedException();
        //var list = await PlanStateRepo.GetPlanOverState(0.1F);

        //foreach (var item in list)
        //{
        //    SocialSrv.FinishServiceAlert(item.Username);
        //}
    }
}
