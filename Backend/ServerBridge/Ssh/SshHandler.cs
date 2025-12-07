using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.ErrorHandler;
using PhotonBypass.ServerBridge.Services;
using Renci.SshNet;

namespace PhotonBypass.ServerBridge.Ssh;

class SshHandler : ISshHandler
{
    public async Task<SshClient> ConnectTo(ServerEntity server)
    {
        var config = server.Config?.SshConfig ??
                     throw new Exception($"The ssh configuration is not set for server ({server.Id}:{server.Name})");
        
        var node = new SshClient(server.IpAddress, config.Port, config.Username, config.Password);

        await node.ConnectAsync(CancellationToken.None);

        if (node.IsConnected) return node;
        
        node.Dispose();
        throw new UserException("خطای اتصال به سرور!",
            $"Cannot connect to server: {server.Name}={server.IpAddress}");
    }
}
