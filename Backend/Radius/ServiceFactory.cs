using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Radius.Repository;
using PhotonBypass.Radius.Repository.DbContext;
using PhotonBypass.Radius.WebService;
using PhotonBypass.Tools;

namespace PhotonBypass.Radius;

public static class ServiceFactory
{
    public static void AddRadiuservices(this IServiceCollection services)
    {
        services.AddLazySingleton<RadiusServiceOptions>();
        services.AddLazySingleton<RadDapperOptions>();
        services.AddLazySingleton<RadDbContext>();

        services.AddLazyTransient<ICloudRepository, CloudRepository>();
        services.AddLazyTransient<INasRepository, NasRepository>();
        services.AddLazyTransient<IPermanentUsersRepository, PermanentUsersRepository>();
        services.AddLazyTransient<IProfileRepository, ProfileRepository>();
        services.AddLazyTransient<IRadAcctRepository, RadAcctRepository>();
        services.AddLazyTransient<IRealmRepository, RealmRepository>();
        services.AddLazyTransient<ITopUpRepository, TopUpRepository>();
        services.AddLazyTransient<IUserPlanStateRepository, UserPlanStateRepository>();

        services.AddLazySingleton<IStaticRepository, StaticRepository>();
        services.AddLazySingleton<IRadiusService, RadiusDeskService>();
    }
}
