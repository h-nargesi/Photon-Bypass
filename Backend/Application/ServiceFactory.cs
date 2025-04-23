using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Application.Account;
using PhotonBypass.Application.Authentication;
using PhotonBypass.Application.Basics;
using PhotonBypass.Application.Connection;
using PhotonBypass.Application.Plan;
using PhotonBypass.Application.Vpn;

namespace PhotonBypass.Application;

public static class ServiceFactory
{
    public static void AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IAccountApplication, AccountApplication>();
        services.AddScoped<IAuthApplication, AuthApplication>();
        services.AddScoped<IBasicsApplication, BasicsApplication>();
        services.AddScoped<IConnectionApplication, ConnectionApplication>();
        services.AddScoped<IPlanApplication, PlanApplication>();
        services.AddScoped<IVpnApplication, VpnApplication>();
    }
}
