using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.ErrorHandler;
using PhotonBypass.ServerBridge.Services;
using Renci.SshNet;
using Serilog;

namespace PhotonBypass.ServerBridge.Ssh;

class SshConnection(SshClient node, ServerEntity server) : ISshConnection
{
    public ServerEntity Server => server;

    public bool Execute(string command, out string result)
    {
        Log.Information("Execute command on server: {0}\n{1}", node.ConnectionInfo.Host, command);

        var execution = node.RunCommand(command);
        result = execution.Result;

        Log.Debug("Result command on server: {0}\n{1}", node.ConnectionInfo.Host, result);

        if (string.IsNullOrEmpty(execution.Error)) return true;

        throw new UserException("خطای اجرای دستور در سرور!",
            $"Cannot connect to server: {node.ConnectionInfo.Host}\n{execution.Error}");
    }

    public void Dispose()
    {
        node?.Dispose();
        GC.SuppressFinalize(this);
    }
}
