using Microsoft.Extensions.Hosting;
using PhotonBypass.FreeRadius.Interfaces;
using PhotonBypass.FreeRadius.Repository;
using PhotonBypass.FreeRadius.Repository.DbContext;
using PhotonBypass.FreeRadius.WebService;
using PhotonBypass.Tools;

namespace PhotonBypass.FreeRadius;

public static class ServiceFactory
{
    public static void AddRadiusServices<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.BindValidateReturn<RadiusServiceOptions>(builder.Configuration);
        builder.Services.BindValidateReturn<RadDapperOptions>(builder.Configuration);
        builder.Services.AddLazySingleton<RadDbContext>();

        // builder.Services.AddLazyTransient<ICloudRepository, CloudRepository>();
        // builder.Services.AddLazyTransient<INasRepository, NasRepository>();
        // builder.Services.AddLazyTransient<IPermanentUsersRepository, PermanentUsersRepository>();
        // builder.Services.AddLazyTransient<IProfileRepository, ProfileRepository>();
        // builder.Services.AddLazyTransient<IRadAcctRepository, RadAcctRepository>();
        // builder.Services.AddLazyTransient<IRealmRepository, RealmRepository>();
        // builder.Services.AddLazyTransient<ITopUpRepository, TopUpRepository>();
        // builder.Services.AddLazyTransient<IUserPlanStateRepository, UserPlanStateRepository>();

        builder.Services.AddLazySingleton<IStaticRepository, StaticRepository>();
        builder.Services.AddLazySingleton<IRadiusService, RadiusDeskService>();
    }
}
