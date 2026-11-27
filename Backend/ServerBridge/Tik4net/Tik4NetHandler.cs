using Microsoft.Extensions.Options;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.ErrorHandler;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.ServerBridge.Tunnel;
using tik4net;

namespace PhotonBypass.ServerBridge.Tik4net;

/// <summary>
/// Opens a User Manager API connection to a server over its configured
/// transport (Docs/25 D3): the direct transport addresses the configured
/// host, the tunnel transport addresses the loopback port of the pooled
/// session's port-forward.
/// </summary>
class Tik4NetHandler(
    IOptions<ServerBridgeOptions> options,
    Lazy<ISshTunnelPool> pool,
    TikConnectionFactory connection_factory) : ITik4NetHandler
{
    private const int ApiDefaultPort = 8728;

    private const int ApiSslDefaultPort = 8729;

    private const string LoopbackHost = "127.0.0.1";

    public async Task<ITikConnection> ConnectTo(ServerEntity server)
    {
        // Config decrypts and parses on every access: read it once per call
        var configuration = server.Config;

        var config = configuration?.WebApiConfig ??
                     throw new Exception($"The web-api configuration is not set for server ({server.Id}:{server.Name})");

        var timeout = TimeSpan.FromSeconds(options.Value.Tik4NetOpenTimeoutSeconds);

        var service_port = config.Port ?? (config.Ssl ? ApiSslDefaultPort : ApiDefaultPort);

        if (configuration!.Transport != ServerTransport.SshTunnel)
        {
            return await OpenDirectAsync(config, config.HostName ?? string.Empty, service_port, timeout);
        }

        // the session outlives the connection: the forward must stay up until the caller closes it
        var session = await pool.Value.GetAsync(server);

        return await OpenTunnelAsync(server, session, config, service_port, session.ForwardedPort(service_port), timeout);
    }

    private async Task<ITikConnection> OpenDirectAsync(WebApiConfig config, string host, int port, TimeSpan timeout)
    {
        var connection = connection_factory(config.Ssl ? TikConnectionType.ApiSsl : TikConnectionType.Api);

        try
        {
            await TikConnectionOpener.OpenAsync(connection, host, port,
                config.Username ?? string.Empty, config.Password ?? string.Empty, timeout);
        }
        catch (TimeoutException)
        {
            connection.Dispose();

            throw new UserException(SshErrors.ConnectMessage,
                $"Cannot connect to server (timeout after {timeout.TotalSeconds:N0}s): {config.HostName}");
        }
        catch
        {
            connection.Dispose();

            throw;
        }

        return connection;
    }

    private async Task<ITikConnection> OpenTunnelAsync(
        ServerEntity server, ISshTunnelSession session, WebApiConfig config, int service_port, int local_port, TimeSpan timeout)
    {
        var connection = connection_factory(config.Ssl ? TikConnectionType.ApiSsl : TikConnectionType.Api);

        try
        {
            await TikConnectionOpener.OpenAsync(
                connection, LoopbackHost, local_port,
                config.Username ?? string.Empty, config.Password ?? string.Empty, timeout);
        }
        catch (TikConnectionLoginException)
        {
            // the service answered through the channel and rejected the credentials: the tunnel session is healthy
            connection.Dispose();

            throw;
        }
        catch (TimeoutException)
        {
            connection.Dispose();

            pool.Value.Fault(server.Id, session.Generation, "User Manager API open timeout");

            throw new UserException(SshErrors.ConnectMessage,
                $"Cannot reach the User Manager API (service port {service_port}) of server ({server.Id}:{server.Name}) through the tunnel within {timeout.TotalSeconds:N0}s");
        }
        catch (Exception error)
        {
            // an accepted-then-closed connection: the server may have refused the forward (Docs/25 appendix B, finding 3)
            pool.Value.Fault(server.Id, session.Generation, $"User Manager API open failed ({error.GetType().Name})");

            connection.Dispose();

            throw new UserException(SshErrors.ConnectMessage,
                $"Cannot open the User Manager API (service port {service_port}) through the tunnel of server ({server.Id}:{server.Name}): {error.GetType().Name}");
        }

        return connection;
    }
}
