using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Application.Account;
using PhotonBypass.Application.Authentication;

namespace PhotonBypass.Application;

public static class ServiceFactory
{
    public static void AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAccountApplication, AccountApplication>();
        services.AddScoped<IAuthApplication, AuthApplication>();
    }
}
