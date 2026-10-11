using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Plan;
using Quartz;

namespace PhotonBypass.Application.Management;

[DisallowConcurrentExecution]
public class JobInterval(IServiceProvider service_provider) : IJob
{
    public const int IntervalInMinutes = 60;

    public async Task Execute(IJobExecutionContext context)
    {
        using var scope_main = service_provider.CreateScope();
        var server_mng_srv = scope_main.ServiceProvider.GetRequiredService<IServerManagementService>();

        await server_mng_srv.UpdateTrafficData();

        var plan_state_repo = scope_main.ServiceProvider.GetRequiredService<IPlanStateRepository>();

        var plan_state_list = await plan_state_repo.GetAll();

        if (plan_state_list.Count < 1)
        {
            await RunWalletThresholdRule(scope_main);
            return;
        }

        // The three parallel services must keep separate scopes: repositories are
        // scoped and share one SqlConnection per scope, so a shared scope would
        // make their parallel database calls collide on the same connection.
        using var scope_notif = service_provider.CreateScope();
        using var scope_abandoned = service_provider.CreateScope();
        using var scope_radius = service_provider.CreateScope();

        var account_mng_srv_1 = scope_notif.ServiceProvider.GetRequiredService<IAccountMonitoringService>();
        var account_mng_srv_2 = scope_abandoned.ServiceProvider.GetRequiredService<IAccountMonitoringService>();
        var account_radius_srv = scope_radius.ServiceProvider.GetRequiredService<IAccountRadiusSyncService>();

        await Task.WhenAll(
            account_mng_srv_1.NotifSendServices(plan_state_list),
            account_radius_srv.DeactivateInvalidRadiusUsers(plan_state_list),
            account_mng_srv_2.InactiveAbandonedUsers(plan_state_list),
            server_mng_srv.CheckUserServerBalance());

        await RunWalletThresholdRule(scope_main);
    }

    private static async Task RunWalletThresholdRule(IServiceScope scope_main)
    {
        var wallet_threshold_srv = scope_main.ServiceProvider
            .GetRequiredService<IWalletThresholdService>();

        await wallet_threshold_srv.CheckAndApply();
    }

}