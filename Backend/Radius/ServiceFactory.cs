using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Radius.Repository;
using PhotonBypass.Radius.Repository.DbContext;

namespace PhotonBypass.Radius;

public static class ServiceFactory
{
    public static void AddDapperDbContext(this IServiceCollection services)
    {
        services.AddSingleton<RadDapperOptions>();
        services.AddSingleton<RadDbContext>();

        services.AddTransient<ICloudRepository, ICloudRepository>();
        services.AddTransient<INasRepository, NasRepository>();
        services.AddTransient<IPermenantUsersRepository, PermenantUsersRepository>();
        services.AddTransient<IProfileRepository, ProfileRepository>();
        services.AddTransient<IRadAcctRepository, RadAcctRepository>();
        services.AddTransient<IStaticRepository, StaticRepository>();
    }
}
