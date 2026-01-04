using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhotonBypass.ServerBridge.Api;
using PhotonBypass.ServerBridge.Email;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.ServerBridge.Ssh;
using PhotonBypass.ServerBridge.Tik4net;
using PhotonBypass.Tools;

namespace PhotonBypass.ServerBridge;

public static class ServiceFactory
{
    public static void AddServerBridgeServices<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddLazyScoped<IApiHandler, ApiHandler>();
        builder.Services.AddLazyScoped<IEmailHandler, EmailHandler>();
        builder.Services.AddSingleton<ISshHandler, SshHandler>();
        builder.Services.AddSingleton<ITik4NetHandler, Tik4NetHandler>();
        builder.Services.AddSingleton<ResourceSynchronization>();
    }
}
