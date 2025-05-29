using Microsoft.Extensions.DependencyInjection;

namespace PhotonBypass.Tools;

public static class LazyDependencyInjections
{
    public static void AddLazyScoped<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        services.AddScoped<TService, TImplementation>();
        services.AddScoped(provider => new Lazy<TService>(() => provider.GetRequiredService<TService>()));
    }

    public static void AddLazyScoped<TService>(this IServiceCollection services, Func<IServiceProvider, TService> provider)
        where TService : class
    {
        services.AddScoped(provider);
        services.AddScoped(provider => new Lazy<TService>(() => provider.GetRequiredService<TService>()));
    }

    public static void AddLazyTransient<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        services.AddTransient<TService, TImplementation>();
        services.AddTransient(provider => new Lazy<TService>(() => provider.GetRequiredService<TService>()));
    }

    public static void AddLazySingleton<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        services.AddSingleton<TService, TImplementation>();
        services.AddSingleton(provider => new Lazy<TService>(() => provider.GetRequiredService<TService>()));
    }

    public static void AddLazySingleton<TService>(this IServiceCollection services)
        where TService : class
    {
        services.AddSingleton<TService>();
        services.AddSingleton(provider => new Lazy<TService>(() => provider.GetRequiredService<TService>()));
    }
}
