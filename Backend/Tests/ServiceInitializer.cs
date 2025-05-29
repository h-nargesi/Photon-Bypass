using Microsoft.Extensions.Hosting;
using PhotonBypass.API;

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

    public void Build(HostApplicationBuilder builder)
    {
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
    }
}
