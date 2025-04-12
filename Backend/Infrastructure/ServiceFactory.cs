using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Infra.Database.Local;
using PhotonBypass.Infra.Database.Radius;

namespace PhotonBypass.Infra;

public static class ServiceFactory
{
    public static void AddDapperDbContext(this IServiceCollection services)
    {
        services.AddSingleton<LocalDapperOptions>();
        services.AddSingleton<LocalDbContext>();
        services.AddSingleton<RadDapperOptions>();
        services.AddSingleton<RadDbContext>();

        services.AddTransient(typeof(ILocalRepository<>), typeof(LocalRepository<>));
        services.AddTransient(typeof(IRadRepository<>), typeof(RadRepository<>));
    }
}
