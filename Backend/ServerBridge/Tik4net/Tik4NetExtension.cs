using PhotonBypass.Domain.Servers.Entity;
using tik4net;

namespace PhotonBypass.ServerBridge.Tik4net;

public static class Tik4NetExtension
{
    public static async Task<ITikConnection> TikApiConnect(this ServerEntity server)
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
