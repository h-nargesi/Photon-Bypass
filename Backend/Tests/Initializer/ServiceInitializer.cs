using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using PhotonBypass.Admin;
using PhotonBypass.Portal;

namespace PhotonBypass.Test.Initializer;

public class ServiceInitializer
{
    protected readonly IHost App;

    protected ServiceInitializer(params Type[] types)
    {
        App = Initialize(types);
    }

    private WebApplication Initialize(Type[] types)
    {
        var builder = WebApplication.CreateBuilder()
            .AddAppServices()
            .AddPortalServices()
            .AddAdminServices();

        AddDefaultServices(builder, types.ToHashSet());
        AddClassTestServices(builder);

        return builder.Build();
    }

    private static void AddDefaultServices(WebApplicationBuilder builder, HashSet<Type> mock_types)
    {
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a =>
            {
                try
                {
                    return a.GetTypes();
                }
                catch
                {
                    return [];
                }
            })
            .Where(t => t is { IsClass: true, IsAbstract: false } &&
                        mock_types.Any(m => m.IsAssignableFrom(t)))
            .ToList();

        foreach (var initializer in types.Select(type => type.GetMethod("CreateInstance")))
        {
            initializer?.Invoke(null, [builder.Services]);
        }
    }

    protected virtual void AddClassTestServices(IHostApplicationBuilder builder)
    {
    }

    public virtual void Dispose()
    {
        App.Dispose();
    }
}