using Microsoft.Extensions.Configuration;
using Serilog;

namespace PhotonBypass;

public static class LogConfiguration
{
    public static void InitializeLogService(IConfiguration configuration)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        Log.Information("Starting up ...");
    }
}
