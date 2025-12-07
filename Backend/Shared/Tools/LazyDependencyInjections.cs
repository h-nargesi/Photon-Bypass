using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PhotonBypass.Tools;

public static class LazyDependencyInjections
{
    public static void AddLazyScoped<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        services.AddScoped<TService, TImplementation>();
        services.AddScoped(provider => new Lazy<TService>(provider.GetRequiredService<TService>));
    }

    public static void AddLazyScoped<TService>(this IServiceCollection services, Func<IServiceProvider, TService> provider_func)
        where TService : class
    {
        services.AddScoped(provider_func);
        services.AddScoped(provider => new Lazy<TService>(provider.GetRequiredService<TService>));
    }

    public static void AddLazyTransient<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        services.AddTransient<TService, TImplementation>();
        services.AddTransient(provider => new Lazy<TService>(provider.GetRequiredService<TService>));
    }

    public static void AddLazyTransient<TService>(this IServiceCollection services, Func<IServiceProvider, TService> provider_func)
        where TService : class
    {
        services.AddTransient(provider_func);
        services.AddTransient(provider => new Lazy<TService>(provider.GetRequiredService<TService>));
    }

    public static void AddLazySingleton<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        services.AddSingleton<TService, TImplementation>();
        services.AddSingleton(provider => new Lazy<TService>(provider.GetRequiredService<TService>));
    }

    public static void AddLazySingleton<TService>(this IServiceCollection services, Func<IServiceProvider, TService> provider_func)
        where TService : class
    {
        services.AddSingleton(provider_func);
        services.AddSingleton(provider => new Lazy<TService>(provider.GetRequiredService<TService>));
    }

    public static void BindValidateReturn<TOptions>(this IServiceCollection services) where TOptions : class
    {
        services.AddOptions<TOptions>()
            .BindConfiguration(typeof(TOptions).Name)
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }

    public static TOptions LoadOptions<TOptions>(this IConfiguration configuration, string key)
    {
        return configuration.GetSection(key).Get<TOptions>() ?? throw new Exception($"Key option not found: {key}");
    }
}
