using Microsoft.Extensions.DependencyInjection;

namespace PhotonBypass;

public static class DependencyInjection
{
    public static event AddServicesHandler? OnAddServices;

    public static void AddServices(this IServiceCollection services)
    {
        OnAddServices?.Invoke(services);
    }
}

public delegate void AddServicesHandler(IServiceCollection services);
