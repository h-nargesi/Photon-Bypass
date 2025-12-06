using Microsoft.Extensions.Hosting;
using PhotonBypass.Tools;

namespace PhotonBypass.ServerBridge;

public static class ServiceFactory
{
    public static void AddServerBridgeServices<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddLazyScope<IApiHandler, ApiHandler>();
        builder.Services.AddLazySingleton<ISshHandler, SshHandler>();
        builder.Services.AddLazySingleton<ITik4NetHandler, Tik4NetHandler>();
        builder.Services.AddLazySingleton<IEmailHandler, EmailHandler>();
    }
}
