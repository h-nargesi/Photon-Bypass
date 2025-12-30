using Microsoft.Extensions.Hosting;
using PhotonBypass.Infra.Nas;
using PhotonBypass.Infra.Radius;
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

        builder.Services.AddLazyKeyedTransient<IInfraSessionRadiusSyncService, SessionRadiusSyncUserManagerService>(RadiusType.UserManager);
        builder.Services.AddLazyKeyedTransient<IInfraAccountRadiusSyncService, AccountRadiusSyncUserManagerService>(RadiusType.UserManager);
        builder.Services.AddLazyTransient<IMikrotikDirectService, MikrotikDirectService>();
    }
}