using Microsoft.Extensions.Hosting;
using PhotonBypass.API;
using PhotonBypass.Test.MockOutSources;
using System.Reflection;

namespace PhotonBypass.Test;

public class ServiceInitializer : IDisposable
{
    private IHost? host;
    private IServiceScope? scope;

    public static HostApplicationBuilder Initialize()
    {
        return Host.CreateApplicationBuilder()
            .AddAppServices();
    }

    public static void AddDefaultServices(HostApplicationBuilder builder)
    {
        var mock_types = typeof(IOutSourceMoq);
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch { return []; }
            })
            .Where(t => mock_types.IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
            .ToList();

        foreach (var type in types)
        {
            var initializer = type.GetMethod("CreateInstance");
            initializer?.Invoke(null, [builder.Services]);
        }
    }

    public void Build(HostApplicationBuilder? builder = null)
    {
        builder ??= Initialize();
        host = builder.Build();
    }

    public IServiceProvider CreateScope()
    {
        if (host == null) throw new ArgumentNullException(nameof(host));
        scope = host.Services.CreateScope();
        return scope.ServiceProvider;
    }

    public void Dispose()
    {
        host?.Dispose();
        scope?.Dispose();
        GC.SuppressFinalize(this);
    }
}
