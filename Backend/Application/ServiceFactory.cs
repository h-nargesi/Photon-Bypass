using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Application.Account;
using PhotonBypass.Application.Authentication;
using PhotonBypass.Application.Database;

namespace PhotonBypass.Application;

public static class ServiceFactory
{
    public static void AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAccountApplication, AccountApplication>();
        services.AddScoped<IAuthApplication, AuthApplication>();

        services.AddTransient<AccountRepository>();
        services.AddTransient<HistoryRepository>();
        services.AddTransient<ResetPassRepository>();

        services.AddTransient<PermenantUsersRepository>();
        services.AddTransient<RadAcctRepository>();
        services.AddTransient<NasRepository>();
        services.AddSingleton<StaticRepository>();
    }
}
