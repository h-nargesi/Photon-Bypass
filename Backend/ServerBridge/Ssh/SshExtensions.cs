using PhotonBypass.ErrorHandler;
using Renci.SshNet;
using Serilog;

namespace PhotonBypass.ServerBridge.Ssh;

public static class SshExtensions
{
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
