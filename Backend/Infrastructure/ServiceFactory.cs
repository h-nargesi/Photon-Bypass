using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Servers;
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
        builder.Services.AddScoped<LocalDbContext>();

        builder.Services.AddLazyTransient<IAccountRepository, AccountRepository>();
        builder.Services.AddLazyTransient<IHistoryRepository, HistoryRepository>();
        builder.Services.AddLazyTransient<IResetPassRepository, ResetPassRepository>();
        builder.Services.AddLazyTransient<IRenewalRepository, RenewalRepository>();
        builder.Services.AddLazyTransient<ITrafficDataRepository, TrafficDataRepository>();
        builder.Services.AddLazyTransient<IServerRepository, ServerRepository>();
        builder.Services.AddLazyTransient<IRealmRepository, RealmRepository>();
        builder.Services.AddLazyTransient<IPriceRepository, PriceRepository>();

        builder.Services.AddLazySingleton<IPriceCalculator, PriceCalculator>();
    }
}
