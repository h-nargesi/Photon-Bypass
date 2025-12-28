using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Plan;
using Quartz;

namespace PhotonBypass.Application.Management.Model;

public class JobInterval(IServiceProvider service_provider) : IJob
{
    public const int IntervalInMinutes = 60;

    public async Task Execute(IJobExecutionContext context)
    {
        var scope_main = service_provider.CreateScope();
        var server_mng_srv = scope_main.ServiceProvider.GetRequiredService<IServerManagementService>();
        
        await server_mng_srv.UpdateTrafficData();

        var plan_state_repo = scope_main.ServiceProvider.GetRequiredService<IPlanStateRepository>();
        
        var plan_state_list = await plan_state_repo.GetAll();

        if (plan_state_list.Count < 1)
        {
            return;
        }

        var account_mng_srv_1 = service_provider.CreateScope()
            .ServiceProvider.GetRequiredService<IAccountMonitoringService>();

        var account_mng_srv_2 = service_provider.CreateScope()
            .ServiceProvider.GetRequiredService<IAccountMonitoringService>();

        var account_radius_srv = service_provider.CreateScope()
            .ServiceProvider.GetRequiredService<IAccountRadiusSyncService>();

        Task.WaitAll(
            account_mng_srv_1.NotifSendServices(plan_state_list),
            account_radius_srv.DeactivateInvalidRadiusUsers(plan_state_list),
            account_mng_srv_2.InactiveAbandonedUsers(plan_state_list),
            server_mng_srv.CheckUserServerBalance());
    }

}