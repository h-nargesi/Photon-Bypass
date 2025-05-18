using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Radius.Repository;
using PhotonBypass.Radius.Repository.DbContext;
using PhotonBypass.Radius.WebService;

namespace PhotonBypass.Radius;

static class ServiceFactory
{
    static ServiceFactory()
    {
        DependencyInjection.OnAddServices += AddServices;
    }

    static void AddServices(this IServiceCollection services)
    {
        services.AddSingleton<RadiusServiceOptions>();
        services.AddSingleton<RadDapperOptions>();
        services.AddSingleton<RadDbContext>();

        services.AddTransient<ICloudRepository, CloudRepository>();
        services.AddTransient<INasRepository, NasRepository>();
        services.AddTransient<IPermanentUsersRepository, PermanentUsersRepository>();
        services.AddTransient<IProfileRepository, ProfileRepository>();
        services.AddTransient<IRadAcctRepository, RadAcctRepository>();
        services.AddTransient<IRealmRepository, RealmRepository>();
        services.AddTransient<ITopUpRepository, TopUpRepository>();

        services.AddSingleton<IStaticRepository, StaticRepository>();
        services.AddSingleton<IRadiusService, RadiusDeskService>();
    }
}
