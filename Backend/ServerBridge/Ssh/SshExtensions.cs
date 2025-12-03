using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.ErrorHandler;
using Renci.SshNet;
using Serilog;

namespace PhotonBypass.ServerBridge.Ssh;

public static class SshExtensions
{
    public static async Task<SshClient> SshConnect(this ServerEntity server)
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

    public static bool Execute(this SshClient node, string command, out string result)
    {
        Log.Information("Execute command on server: {0}\n{1}", node.ConnectionInfo.Host, command);

        var execution = node.RunCommand(command);
        result = execution.Result;

        Log.Debug("Result command on server: {0}\n{1}", node.ConnectionInfo.Host, result);

        if (string.IsNullOrEmpty(execution.Error)) return true;
        
        throw new UserException("خطای اجرای دستور در سرور!",
            $"Cannot connect to server: {node.ConnectionInfo.Host}\n{execution.Error}");
    }
}
