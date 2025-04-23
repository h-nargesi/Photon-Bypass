using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Radius.Repository;
using PhotonBypass.Radius.Repository.DbContext;

namespace PhotonBypass.Radius;

static class ServiceFactory
{
    static ServiceFactory()
    {
        DependencyInjection.OnAddServices += AddLocalDbContext;
    }

    static void AddLocalDbContext(this IServiceCollection services)
    {
        services.AddSingleton<RadDapperOptions>();
        services.AddSingleton<RadDbContext>();

        services.AddTransient<ICloudRepository, ICloudRepository>();
        services.AddTransient<INasRepository, NasRepository>();
        services.AddTransient<IPermanentUsersRepository, PermanentUsersRepository>();
        services.AddTransient<IProfileRepository, ProfileRepository>();
        services.AddTransient<IRadAcctRepository, RadAcctRepository>();

        services.AddSingleton<IStaticRepository, StaticRepository>();
    }
}
