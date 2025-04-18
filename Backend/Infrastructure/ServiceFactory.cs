using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Domain.Account;
using PhotonBypass.Infra.Repository;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra;

public static class ServiceFactory
{
    public static void AddDapperDbContext(this IServiceCollection services)
    {
        services.AddSingleton<LocalDapperOptions>();
        services.AddSingleton<LocalDbContext>();

        services.AddTransient<IAccountRepository, AccountRepository>();
        services.AddTransient<IHistoryRepository, HistoryRepository>();
        services.AddTransient<IResetPassRepository, ResetPassRepository>();
    }
}
