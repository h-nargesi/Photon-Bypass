using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Domain.Services;

namespace PhotonBypass.OutSource;

static class ServiceFactory
{
    static ServiceFactory()
    {
        DependencyInjection.OnAddServices += AddLocalDbContext;
    }

    static void AddLocalDbContext(this IServiceCollection services)
    {
        services.AddSingleton<IEmailService, EmailService>();
    }
}
