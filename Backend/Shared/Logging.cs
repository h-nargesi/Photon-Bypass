using Serilog;
using Serilog.Events;

namespace PhotonBypass;

public static class LogConfiguration
{
    public static void InitializeLogService(bool is_development, string file_path)
    {
        var file_event_level = is_development ? LogEventLevel.Debug : LogEventLevel.Information;

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(LogEventLevel.Debug)
            .WriteTo.File(file_path, file_event_level, 
                rollingInterval: RollingInterval.Day, 
                rollOnFileSizeLimit: true,
                buffered: true)
            .CreateLogger();

        Log.Information("Starting up ...");
    }
}
