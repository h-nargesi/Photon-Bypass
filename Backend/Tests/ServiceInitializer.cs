using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using PhotonBypass.API;
using PhotonBypass.Test.MockOutSources;

namespace PhotonBypass.Test;

public abstract class ServiceInitializer : IDisposable
{
    protected readonly IHost App;

    protected ServiceInitializer(params Type[] types)
    {
        App = Initialize(types);
    }

    private WebApplication Initialize(Type[] types)
    {
        var builder = WebApplication.CreateBuilder()
            .AddAppServices();

        AddDefaultServices(builder, types.ToHashSet());
        AddServices(builder);

        return builder.Build();
    }

    private static void AddDefaultServices(WebApplicationBuilder builder, HashSet<Type> mock_types)
    {
        mock_types.Add(typeof(IOutSourceMoq));
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

    protected virtual void AddServices(IHostApplicationBuilder builder)
    {
    }

    public void Dispose()
    {
        App.Dispose();
        GC.SuppressFinalize(this);
    }
}