using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhotonBypass.Infra.Radius.UserManager;
using PhotonBypass.Tools;

namespace PhotonBypass.Mikrotik.Radius;

public static class ServiceFactory
{
    public static void AddMikrotikRadiusServices<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // builder.Services.AddScoped<MikrotikApiCall>();
        // builder.Services.AddHttpClient(MikrotikApiCall.HttpClientKeyName, client =>
        // {
        //     client.DefaultRequestHeaders.Accept.Add(
        //         new MediaTypeWithQualityHeaderValue("application/json"));
        // });
        
        builder.Services.AddLazyTransient<ISessionRadiusSyncService, SessionRadiusSyncService>();
        builder.Services.AddLazyTransient<IAccountRadiusSyncService, AccountRadiusSyncService>();
    }
}