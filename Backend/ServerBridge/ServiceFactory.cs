using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PhotonBypass.Domain.Servers;
using PhotonBypass.ServerBridge.Api;
using PhotonBypass.ServerBridge.Email;
using PhotonBypass.ServerBridge.HostChecks;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.ServerBridge.Ssh;
using PhotonBypass.ServerBridge.Tik4net;
using PhotonBypass.ServerBridge.Tunnel;
using PhotonBypass.Tools;
using tik4net;

namespace PhotonBypass.ServerBridge;

public static class ServiceFactory
{
    public static void AddServerBridgeServices<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.BindValidateReturn<ServerBridgeOptions>();

        // conditional rule: an Enabled transport must be fully configured,
        // which data annotations cannot express
        builder.Services.AddOptions<EmailOptions>()
            .BindConfiguration(nameof(EmailOptions))
            .Validate(EmailOptionsValidation.IsValid,
                "EmailOptions: when Enabled is true, Address, Password and Host must be set. See Docs/19-deploy-runbook.md.")
            .ValidateOnStart();

        // conditional rule: every host-check target is a literal host:port pair
        builder.Services.AddOptions<ServerBridgeOptions>()
            .Validate(options => options.HostCheckTargets.All(HostCheckTarget.IsValid), HostCheckTarget.InvalidMessage)
            .ValidateOnStart();

        builder.Services.AddLazyScoped<IApiHandler, ApiHandler>();
        builder.Services.AddLazyScoped<IEmailHandler, EmailHandler>();
        // singleton so every scoped EmailHandler shares one hourly window
        builder.Services.AddSingleton(sp => new EmailSendLimiter(
            sp.GetRequiredService<IOptions<EmailOptions>>().Value.MaxSendsPerHour));

        builder.Services.TryAddSingleton(TimeProvider.System);

        // Docs/25 D3–D5: one SSH seam for both transports; tests replace the connector with a fake
        builder.Services.AddSingleton<ISshConnector, SshConnector>();
        builder.Services.AddLazySingleton<ISshTunnelPool, SshTunnelPool>();
        // the same singleton instance also runs the idle sweep
        builder.Services.AddHostedService(provider => (SshTunnelPool)provider.GetRequiredService<ISshTunnelPool>());
        builder.Services.AddSingleton<ISshHandler, SshHandler>();
        builder.Services.AddSingleton<TikConnectionFactory>(type => ConnectionFactory.CreateConnection(type));
        builder.Services.AddSingleton<ITik4NetHandler, Tik4NetHandler>();
        builder.Services.AddSingleton<ResourceSynchronization>();

        // Docs/25 D8–D9: the connection test; the channel probe never uses the pool
        builder.Services.AddSingleton<IServerChannelProbe, ServerChannelProbe>();

        // Docs/25 D11: host-capability checks at every start, on demand and by heartbeat
        builder.Services.AddSingleton<HostCapabilityChecks>();
        builder.Services.AddSingleton<IHostCapabilityChecks>(provider => provider.GetRequiredService<HostCapabilityChecks>());
        builder.Services.AddHostedService(provider => provider.GetRequiredService<HostCapabilityChecks>());
    }
}
