using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhotonBypass.FreeRadius.Interfaces;
using PhotonBypass.FreeRadius.Repository;
using PhotonBypass.FreeRadius.Repository.DbContext;
using PhotonBypass.FreeRadius.WebService;
using PhotonBypass.Tools;
using System.Net.Http.Headers;
using PhotonBypass.FreeRadius.Application;
using PhotonBypass.Infra.Radius.RadiusDesk;

namespace PhotonBypass.FreeRadius;

public static class ServiceFactory
{
    public const string HttpClientKeyName = "free-radius";

    public static void AddRadiusDeskServices<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHttpClient(HttpClientKeyName, client =>
        {
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            client.BaseAddress = new Uri("cake4/rd_cake");
        });

        builder.Services.AddScoped<RadDbContext>();
        builder.Services.AddScoped<RadWebApiOptionContext>();

        builder.Services.AddLazyTransient<ICloudRepository, CloudRepository>();
        builder.Services.AddLazyTransient<INasRepository, NasRepository>();
        builder.Services.AddLazyTransient<IPermanentUsersRepository, PermanentUsersRepository>();
        builder.Services.AddLazyTransient<IProfileRepository, ProfileRepository>();
        builder.Services.AddLazyTransient<IRadAcctRepository, RadAcctRepository>();
        builder.Services.AddLazyTransient<IRealmRepository, RealmRepository>();
        builder.Services.AddLazyTransient<ITopUpRepository, TopUpRepository>();
        builder.Services.AddLazyTransient<IUserPlanStateRepository, UserPlanStateRepository>();

        builder.Services.AddLazyTransient<ISessionRadiusSyncRadiusDeskService, SessionRadiusSyncRadiusDeskService>();
        builder.Services.AddLazyTransient<IAccountRadiusSyncRadiusDeskService, AccountRadiusSyncRadiusDeskService>();

        builder.Services.AddSingleton<IStaticRepository, StaticRepository>();
        builder.Services.AddLazyScoped<IRadiusService, RadiusDeskService>();
    }
}
