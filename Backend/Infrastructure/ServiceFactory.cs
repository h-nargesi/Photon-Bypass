using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Static;
using PhotonBypass.Domain.Vpn;
using PhotonBypass.Infra.Repository;
using PhotonBypass.Infra.Repository.DbContext;
using PhotonBypass.Infra.Services;
using PhotonBypass.Tools;

namespace PhotonBypass.Infra;

public static class ServiceFactory
{
    public static void AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddLazySingleton<LocalDapperOptions>();
        services.AddLazySingleton<LocalDbContext>();

        services.AddLazyTransient<IAccountRepository, AccountRepository>();
        services.AddLazyTransient<IHistoryRepository, HistoryRepository>();
        services.AddLazyTransient<IResetPassRepository, ResetPassRepository>();
        services.AddLazyTransient<IPriceRepository, PriceRepository>();
        services.AddLazyTransient<ITrafficDataRepository, TrafficDataRepository>();

        services.AddLazySingleton<IPriceCalculator, PriceCalculator>();
    }
}
