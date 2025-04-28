using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Application.Account;
using PhotonBypass.Application.Authentication;
using PhotonBypass.Application.Basics;
using PhotonBypass.Application.Connection;
using PhotonBypass.Application.Management;
using PhotonBypass.Application.Plan;
using PhotonBypass.Application.Vpn;
using Quartz;
using Quartz.Simpl;

namespace PhotonBypass.Application;

public static class ServiceFactory
{
    public static void AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAccountApplication, AccountApplication>();
        services.AddScoped<IAuthApplication, AuthApplication>();
        services.AddScoped<IBasicsApplication, BasicsApplication>();
        services.AddScoped<IConnectionApplication, ConnectionApplication>();
        services.AddScoped<IPlanApplication, PlanApplication>();
        services.AddScoped<IVpnApplication, VpnApplication>();

        services.AddQuartz(quartz =>
        {
            quartz.UseJobFactory<MicrosoftDependencyInjectionJobFactory>();

            var jobKey = new JobKey("AccountMonitoringService");
            quartz.AddJob<AccountMonitoringService>(opts => opts.WithIdentity(jobKey));

            quartz.AddTrigger(opts => opts
                .ForJob(jobKey)
                .WithIdentity("AccountMonitoringService-trigger")
                .WithSimpleSchedule(x => x
                    .WithIntervalInHours(1)
                    .RepeatForever()
                )
            );
        });

        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
    }
}
