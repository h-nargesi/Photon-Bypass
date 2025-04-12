using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Database.Dapper;

namespace PhotonBypass.Infra;

public static class ServiceFactory
{
    public static void AddDapperDbContext(this IServiceCollection services)
    {
        services.AddSingleton<LocalDapperOptions>();
        services.AddSingleton<LocalDbContext>();
        services.AddSingleton<RadDapperOptions>();
        services.AddSingleton<RadDbContext>();
    }
}
