using Microsoft.Extensions.Hosting;
using PhotonBypass.Application;
using PhotonBypass.FreeRadius;
using PhotonBypass.Infra;
using PhotonBypass.OutSource;

namespace PhotonBypass;

public static class ServiceFactoryHandler
{
    public static TBuilder AddAppServices<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.AddInfrastructureServices();
        builder.AddRadiusServices();
        builder.AddOutSourceServices();
        builder.AddApplicationServices();
        
        return builder;
    }
}