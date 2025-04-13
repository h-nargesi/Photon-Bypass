using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Application.Account;
using PhotonBypass.Application.Authentication;

namespace PhotonBypass.Application;

public static class ServiceFactory
{
    public static void AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IAccountApplication, AccountApplication>();
        services.AddSingleton<IAuthApplication, AuthApplication>();
    }
}
