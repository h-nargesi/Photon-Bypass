using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Vpn;
using PhotonBypass.Infra.Radius;
using PhotonBypass.Tools;
using PhotonBypass.Xui.Application;
using PhotonBypass.Xui.WebService;
using Quartz;
using System.Net.Http.Headers;

namespace PhotonBypass.Xui;

public static class ServiceFactory
{
    public const string HttpClientKeyName = "xui-panel";

    public static void AddXuiPanelServices<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHttpClient(HttpClientKeyName, client =>
        {
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        });

        builder.Services.BindValidateReturn<XuiTrafficOptions>();

        builder.Services.AddOptions<XuiTrafficOptions>()
            .Validate(options => options.LockThresholdSeconds < options.IntervalSeconds,
                "XuiTrafficOptions.LockThresholdSeconds must stay below IntervalSeconds, otherwise the realm " +
                "lock races with the collector cadence at the boundary.")
            .ValidateOnStart();

        builder.Services.AddLazyTransient<IXuiApi, XuiApiService>();
        builder.Services.AddLazyTransient<IXuiTrafficSync, XuiTrafficSync>();
        builder.Services.AddLazyTransient<IVpnLinkService, VlessLinkService>();

        builder.Services.AddLazyKeyedTransient<IInfraSessionRadiusSyncService, SessionRadiusSyncXuiService>(RadiusType.Xui);
        builder.Services.AddLazyKeyedTransient<IInfraAccountRadiusSyncService, AccountRadiusSyncXuiService>(RadiusType.Xui);

        builder.Services.AddQuartz(quartz =>
        {
            var interval = builder.Configuration.GetSection(nameof(XuiTrafficOptions)).Get<XuiTrafficOptions>()
                               ?.IntervalSeconds ?? XuiTrafficOptions.DefaultIntervalSeconds;

            var job_key = new JobKey(nameof(XuiTrafficCollector));

            quartz.AddJob<XuiTrafficCollector>(opts => opts.WithIdentity(job_key));

            quartz.AddTrigger(opts => opts
                .ForJob(job_key)
                .WithIdentity($"{nameof(XuiTrafficCollector)}-trigger")
                .WithSimpleSchedule(x => x
                    .WithIntervalInSeconds(interval)
                    .RepeatForever()
                    .WithMisfireHandlingInstructionFireNow()
                )
            );
        });
    }
}
