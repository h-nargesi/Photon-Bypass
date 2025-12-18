using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Static;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository;
using PhotonBypass.Infra.Repository.DbContext;
using PhotonBypass.Infra.Services;
using PhotonBypass.Tools;

namespace PhotonBypass.Infra;

public static class ServiceFactory
{
    public static void AddInfrastructureServices<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSingleton<IEntityEventService, EntityEventService>();
        builder.Services.BindValidateReturn<LocalDapperOptions>();
        builder.Services.AddScoped<LocalDbContext>();

        builder.Services.AddLazyTransient<IAccountRepository, AccountRepository>();
        builder.Services.AddLazyTransient<IHistoryRepository, HistoryRepository>();
        builder.Services.AddLazyTransient<IResetPassRepository, ResetPassRepository>();
        builder.Services.AddLazyTransient<IRenewalRepository, RenewalRepository>();
        builder.Services.AddLazyTransient<ITrafficDataRepository, TrafficDataRepository>();
        builder.Services.AddLazyTransient<IServerRepository, ServerRepository>();
        builder.Services.AddLazyTransient<IPlanStateRepository, PlanStateRepository>();
        builder.Services.AddLazyTransient<IRealmRepository, RealmRepository>();
        builder.Services.AddLazyTransient<IPriceRepository, PriceRepository>();
        builder.Services.AddLazyTransient<IAccountRadiusSyncService, AccountRadiusSyncService>();
        builder.Services.AddLazyTransient<ISessionRadiusSyncService, SessionRadiusSyncService>();

        builder.Services.AddSingleton<IPriceCalculator, PriceCalculator>();
    }
}