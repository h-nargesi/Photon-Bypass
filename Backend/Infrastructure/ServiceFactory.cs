using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Static;
using PhotonBypass.Domain.Vpn;
using PhotonBypass.Infra.Repository;
using PhotonBypass.Infra.Repository.DbContext;
using PhotonBypass.Infra.Services;

namespace PhotonBypass.Infra;

static class ServiceFactory
{
    static ServiceFactory()
    {
        DependencyInjection.OnAddServices += AddLocalDbContext;
    }

    static void AddLocalDbContext(this IServiceCollection services)
    {
        services.AddSingleton<LocalDapperOptions>();
        services.AddSingleton<LocalDbContext>();

        services.AddTransient<IAccountRepository, AccountRepository>();
        services.AddTransient<IHistoryRepository, HistoryRepository>();
        services.AddTransient<IResetPassRepository, ResetPassRepository>();
        services.AddTransient<IPriceRepository, PriceRepository>();
        services.AddTransient<ITrafficDataRepository, TrafficDataRepository>();

        services.AddSingleton<IPriceCalculator, PriceCalculator>();
    }
}
