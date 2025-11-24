using PhotonBypass.Domain.Servers.Model;
using PhotonBypass.ErrorHandler;
using Renci.SshNet;
using Serilog;

namespace PhotonBypass.Mikrotik.Helper;

public static class SshExtensions
{
    public static async Task<SshClient> Connect(this NasEntity server)
    {
        var node = new SshClient(server.IpAddress, server.SshPort, server.SshUsername, server.SshPassword);

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
        
        Log.Error("Error on execute command on server: {0}\n{1}\n{2}", node.ConnectionInfo.Host, command, execution.Error);
        return false;
    }
}
