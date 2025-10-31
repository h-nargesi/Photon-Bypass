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
        services.AddScoped(p => new Lazy<TService>(p.GetRequiredService<TService>));
    }

    public static void AddLazyScoped<TService>(this IServiceCollection services, Func<IServiceProvider, TService> provider)
        where TService : class
    {
        services.AddScoped(provider);
        services.AddScoped(p => new Lazy<TService>(p.GetRequiredService<TService>));
    }

    public static void AddLazyTransient<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        services.AddTransient<TService, TImplementation>();
        services.AddTransient(p => new Lazy<TService>(p.GetRequiredService<TService>));
    }

    public static void AddLazySingleton<TService, TImplementation>(this IServiceCollection services)
        where TService : class
        where TImplementation : class, TService
    {
        services.AddSingleton<TService, TImplementation>();
        services.AddSingleton(p => new Lazy<TService>(p.GetRequiredService<TService>));
    }

    public static void AddLazySingleton<TService>(this IServiceCollection services)
        where TService : class
    {
        services.AddSingleton<TService>();
        services.AddSingleton(p => new Lazy<TService>(p.GetRequiredService<TService>));
    }

    public static void BindValidateReturn<TOptions>(this IServiceCollection services, IConfiguration configuration) where TOptions : class
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
