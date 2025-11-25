using Microsoft.Extensions.Hosting;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Static;
using PhotonBypass.Infra.Repository;
using PhotonBypass.Infra.Repository.DbContext;
using PhotonBypass.Infra.Services;
using PhotonBypass.Tools;

namespace PhotonBypass.Infra;

public static class ServiceFactory
{
    public static void AddInfrastructureServices<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.BindValidateReturn<LocalDapperOptions>(builder.Configuration);
        builder.Services.AddLazySingleton<LocalDbContext>();

        builder.Services.AddLazyTransient<IAccountRepository, AccountRepository>();
        builder.Services.AddLazyTransient<IHistoryRepository, HistoryRepository>();
        builder.Services.AddLazyTransient<IResetPassRepository, ResetPassRepository>();
        builder.Services.AddLazyTransient<IPriceRepository, PriceRepository>();
        // builder.Services.AddLazyTransient<ITrafficDataRepository, TrafficDataRepository>();

        builder.Services.AddLazySingleton<IPriceCalculator, PriceCalculator>();
    }
}
