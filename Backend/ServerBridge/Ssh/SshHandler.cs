using Microsoft.Extensions.Options;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.ErrorHandler;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.ServerBridge.Tunnel;
using Serilog;

namespace PhotonBypass.ServerBridge.Ssh;

/// <summary>
/// Opens a command connection to a server over its configured transport
/// (Docs/25 D3): the direct transport connects with a password on every call,
/// the tunnel transport borrows the pooled key-authenticated session.
/// </summary>
class SshHandler(IOptions<ServerBridgeOptions> options, ISshConnector connector, Lazy<ISshTunnelPool> pool) : ISshHandler
{
    public async Task<ISshConnection> ConnectTo(ServerEntity server)
    {
        var configuration = server.Config ?? throw NoSshConfiguration(server);

        var command_timeout = TimeSpan.FromSeconds(options.Value.SshCommandTimeoutSeconds);

        if (configuration.Transport == ServerTransport.SshTunnel)
        {
            var session = await pool.Value.GetAsync(server);

            return await session.OpenCommandChannelAsync(command_timeout);
        }

        var ssh = configuration.SshConfig ?? throw NoSshConfiguration(server);

        var endpoint = SshEndpointFactory.Create(server, configuration, HostKeyPolicy.Optional, options.Value);

        ISshChannel channel;

        try
        {
            channel = await connector.ConnectAsync(endpoint, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            throw new UserException(SshErrors.ConnectMessage,
                $"Cannot connect to server (timeout after {options.Value.SshConnectTimeoutSeconds}s): {server.Name}={server.IpAddress}");
        }
        catch (SshConnectException error)
        {
            throw SshErrors.ToUserException(error);
        }

        ISshCommandChannel commands;

        try
        {
            commands = await channel.OpenCommandChannelAsync(CancellationToken.None);
        }
        catch
        {
            channel.Dispose();

            throw;
        }

        return new SshConnection(commands, server, command_timeout, () =>
        {
            commands.Cancel();
            commands.Dispose();
            channel.Dispose();
        });
    }

    private static Exception NoSshConfiguration(ServerEntity server) =>
        new($"The ssh configuration is not set for server ({server.Id}:{server.Name})");
}
