using Microsoft.Extensions.Hosting;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Radius.Repository;
using PhotonBypass.Radius.Repository.DbContext;
using PhotonBypass.Radius.WebService;
using PhotonBypass.Tools;

namespace PhotonBypass.Radius;

public static class ServiceFactory
{
    public static void AddRadiuServices<Builder>(this Builder builder) where Builder : IHostApplicationBuilder
    {
        builder.Services.BindValidateReturn<RadiusServiceOptions>(builder.Configuration);
        builder.Services.BindValidateReturn<RadDapperOptions>(builder.Configuration);
        builder.Services.AddLazySingleton<RadDbContext>();

        builder.Services.AddLazyTransient<ICloudRepository, CloudRepository>();
        builder.Services.AddLazyTransient<INasRepository, NasRepository>();
        builder.Services.AddLazyTransient<IPermanentUsersRepository, PermanentUsersRepository>();
        builder.Services.AddLazyTransient<IProfileRepository, ProfileRepository>();
        builder.Services.AddLazyTransient<IRadAcctRepository, RadAcctRepository>();
        builder.Services.AddLazyTransient<IRealmRepository, RealmRepository>();
        builder.Services.AddLazyTransient<ITopUpRepository, TopUpRepository>();
        builder.Services.AddLazyTransient<IUserPlanStateRepository, UserPlanStateRepository>();

        builder.Services.AddLazySingleton<IStaticRepository, StaticRepository>();
        builder.Services.AddLazySingleton<IRadiusService, RadiusDeskService>();
    }
}
