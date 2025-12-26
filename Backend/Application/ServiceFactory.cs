using Microsoft.Extensions.Hosting;
using PhotonBypass.Application.Account;
using PhotonBypass.Application.Authentication;
using PhotonBypass.Application.Basics;
using PhotonBypass.Application.Connection;
using PhotonBypass.Application.Management;
using PhotonBypass.Application.Plan;
using PhotonBypass.Application.Vpn;
using PhotonBypass.Domain.Management;
using PhotonBypass.Tools;
using Quartz;
using Quartz.Simpl;

namespace PhotonBypass.Application;

public static class ServiceFactory
{
    public static void AddApplicationServices<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.BindValidateReturn<ManagementOptions>();

        builder.Services.AddLazyTransient<IAccountApplication, AccountApplication>();
        builder.Services.AddLazyTransient<IAuthApplication, AuthApplication>();
        builder.Services.AddLazyTransient<IBasicsApplication, BasicsApplication>();
        builder.Services.AddLazyTransient<IConnectionApplication, ConnectionApplication>();
        builder.Services.AddLazyTransient<IPlanApplication, PlanApplication>();
        builder.Services.AddLazyTransient<IPaymentApplication, PaymentApplication>();
        builder.Services.AddLazyTransient<IVpnApplication, VpnApplication>();
        builder.Services.AddLazyTransient<IServerManagementService, ServerManagementService>();
        builder.Services.AddLazyTransient<IAccountMonitoringService, AccountMonitoringService>();

        builder.Services.AddQuartz(quartz =>
        {
            quartz.UseJobFactory<MicrosoftDependencyInjectionJobFactory>();

            var job_key = new JobKey("JobInterval");
            quartz.AddJob<JobInterval>(opts => opts.WithIdentity(job_key));

            quartz.AddTrigger(opts => opts
                .ForJob(job_key)
                .WithIdentity("JobInterval-trigger")
                .WithSimpleSchedule(x => x
                    .WithIntervalInMinutes(JobInterval.IntervalInMinutes)
                    .RepeatForever()
                )
            );
        });

        builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
    }
}
