using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Domain.Services;

namespace PhotonBypass.OutSource;

public static class ServiceFactory
{
    public static void AddOutSourceServices(this IServiceCollection services)
    {
        services.AddSingleton<IEmailService, EmailService>();
        services.AddSingleton<IVpnNodeService, VpnNodeService>();
    }
}
