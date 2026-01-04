using Microsoft.AspNetCore.Builder;
using Serilog;

namespace PhotonBypass;

public static class LogConfiguration
{
    public static WebApplicationBuilder AddLogService(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services);

            Log.Information("Starting up ...");
        });

        return builder;
    }
}
