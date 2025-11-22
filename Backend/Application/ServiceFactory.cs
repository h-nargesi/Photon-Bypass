using Microsoft.Extensions.Hosting;
using PhotonBypass.Application.Account;
using PhotonBypass.Application.Authentication;
using PhotonBypass.Application.Basics;
using PhotonBypass.Application.Connection;
using PhotonBypass.Application.Management;
using PhotonBypass.Application.Plan;
using PhotonBypass.Application.Vpn;
using PhotonBypass.Domain.Management;
using PhotonBypass.FreeRadius;
using PhotonBypass.Infra;
using PhotonBypass.OutSource;
using PhotonBypass.Tools;
using Quartz;
using Quartz.Simpl;

namespace PhotonBypass.Application;

public static class ServiceFactory
{
    public static void AddApplicationServices<Builder>(this Builder builder) where Builder : IHostApplicationBuilder
    {
        builder.AddInfrastructureServices();
        builder.AddRadiuServices();
        builder.AddOutSourceServices();

        builder.Services.BindValidateReturn<ManagementOptions>(builder.Configuration);

        builder.Services.AddLazyScoped<IAccountApplication, AccountApplication>();
        builder.Services.AddLazyScoped<IAuthApplication, AuthApplication>();
        builder.Services.AddLazyScoped<IBasicsApplication, BasicsApplication>();
        builder.Services.AddLazyScoped<IConnectionApplication, ConnectionApplication>();
        builder.Services.AddLazyScoped<IPlanApplication, PlanApplication>();
        builder.Services.AddLazyScoped<IPaymentAppliocation, PaymentAppliocation>();
        builder.Services.AddLazyScoped<IVpnApplication, VpnApplication>();
        builder.Services.AddLazyScoped<IServerManagementService, ServerManagementService>();
        builder.Services.AddLazyScoped<IAccountMonitoringService, AccountMonitoringService>();

        builder.Services.AddQuartz(quartz =>
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

        builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
    }
}
