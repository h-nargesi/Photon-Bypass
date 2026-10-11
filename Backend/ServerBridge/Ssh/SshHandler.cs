using Microsoft.Extensions.Options;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.ErrorHandler;
using PhotonBypass.ServerBridge.Services;
using Renci.SshNet;

namespace PhotonBypass.ServerBridge.Ssh;

class SshHandler(IOptions<ServerBridgeOptions> options) : ISshHandler
{
    public async Task<ISshConnection> ConnectTo(ServerEntity server)
    {
        var config = server.Config?.SshConfig ??
                     throw new Exception($"The ssh configuration is not set for server ({server.Id}:{server.Name})");

        var node = new SshClient(server.IpAddress, config.Port, config.Username, config.Password)
        {
            KeepAliveInterval = TimeSpan.FromSeconds(options.Value.SshKeepAliveIntervalSeconds),
        };

        using var connect_cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.SshConnectTimeoutSeconds));

        try
        {
            await node.ConnectAsync(connect_cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            node.Dispose();
            throw new UserException("خطای اتصال به سرور!",
                $"Cannot connect to server (timeout after {options.Value.SshConnectTimeoutSeconds}s): {server.Name}={server.IpAddress}");
        }

        if (node.IsConnected)
        {
            return new SshConnection(node, server, TimeSpan.FromSeconds(options.Value.SshCommandTimeoutSeconds));
        }

        node.Dispose();
        throw new UserException("خطای اتصال به سرور!",
            $"Cannot connect to server: {server.Name}={server.IpAddress}");
    }
}
