using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Domain.Services;
using PhotonBypass.Tools;

namespace PhotonBypass.OutSource;

public static class ServiceFactory
{
    public static void AddOutSourceServices(this IServiceCollection services)
    {
        services.AddLazySingleton<IEmailService, EmailService>();
        services.AddLazySingleton<IVpnNodeService, VpnNodeService>();
    }
}
