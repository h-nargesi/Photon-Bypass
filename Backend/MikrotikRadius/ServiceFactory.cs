using Microsoft.Extensions.Hosting;
using PhotonBypass.Infra.Nas;
using PhotonBypass.Infra.Radius.UserManager;
using PhotonBypass.Mikrotik.Radius.Application;
using PhotonBypass.Tools;

namespace PhotonBypass.Mikrotik.Radius;

public static class ServiceFactory
{
    //public const string HttpClientKeyName = "mikrotik-api";

    public static void AddMikrotikRadiusServices<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // builder.Services.AddHttpClient(HttpClientKeyName, client =>
        // {
        //     client.DefaultRequestHeaders.Accept.Add(
        //         new MediaTypeWithQualityHeaderValue("application/json"));
        // });

        builder.Services.AddLazyTransient<ISessionRadiusSyncUserManagerService, SessionRadiusSyncUserManagerService>();
        builder.Services.AddLazyTransient<IAccountRadiusSyncUserManagerService, AccountRadiusSyncUserManagerService>();
        builder.Services.AddLazyTransient<IMikrotikDirectService, MikrotikDirectService>();
    }
}