using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.ServerBridge.Services;
using tik4net;

namespace PhotonBypass.ServerBridge.Tik4net;

class Tik4NetHandler : ITik4NetHandler
{
    public async Task<ITikConnection> ConnectTo(ServerEntity server)
    {
        var config = server.Config?.WebApiConfig ??
                     throw new Exception($"The ssh configuration is not set for server ({server.Id}:{server.Name})");

        var connection = ConnectionFactory.CreateConnection(TikConnectionType.ApiSsl);

        if (config.Port.HasValue)
        {
            await connection.OpenAsync(config.HostName, config.Port.Value, config.Username, config.Password);
        }
        else
        {
            await connection.OpenAsync(config.HostName, config.Username, config.Password);
        }

        return connection;
    }
}
