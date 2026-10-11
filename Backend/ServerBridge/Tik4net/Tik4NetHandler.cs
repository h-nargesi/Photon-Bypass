using Microsoft.Extensions.Options;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.ErrorHandler;
using PhotonBypass.ServerBridge.Services;
using tik4net;

namespace PhotonBypass.ServerBridge.Tik4net;

class Tik4NetHandler(IOptions<ServerBridgeOptions> options) : ITik4NetHandler
{
    public async Task<ITikConnection> ConnectTo(ServerEntity server)
    {
        var config = server.Config?.WebApiConfig ??
                     throw new Exception($"The web-api configuration is not set for server ({server.Id}:{server.Name})");

        var connection = ConnectionFactory.CreateConnection(config.Ssl ? TikConnectionType.ApiSsl : TikConnectionType.Api);

        var open_task = config.Port.HasValue
            ? connection.OpenAsync(config.HostName, config.Port.Value, config.Username, config.Password)
            : connection.OpenAsync(config.HostName, config.Username, config.Password);

        var timeout_task = Task.Delay(TimeSpan.FromSeconds(options.Value.Tik4NetOpenTimeoutSeconds));

        if (await Task.WhenAny(open_task, timeout_task) == timeout_task)
        {
            connection.Dispose();
            throw new UserException("خطای اتصال به سرور!",
                $"Cannot connect to server (timeout after {options.Value.Tik4NetOpenTimeoutSeconds}s): {config.HostName}");
        }

        await open_task;

        return connection;
    }
}
