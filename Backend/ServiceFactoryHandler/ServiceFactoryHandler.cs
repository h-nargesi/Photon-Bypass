using Microsoft.Extensions.Hosting;
using PhotonBypass.Application;
using PhotonBypass.FreeRadius;
using PhotonBypass.Infra;
using PhotonBypass.Mikrotik.Radius;
using PhotonBypass.OutSource;

namespace PhotonBypass;

public static class ServiceFactoryHandler
{
    public static TBuilder AddAppServices<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.AddApplicationServices();
        builder.AddInfrastructureServices();
        builder.AddRadiusDeskServices();
        builder.AddMikrotikRadiusServices();
        builder.AddOutSourceServices();
        
        return builder;
    }
}