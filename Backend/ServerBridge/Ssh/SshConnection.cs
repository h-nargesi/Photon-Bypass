using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.ErrorHandler;
using PhotonBypass.ServerBridge.Services;
using Renci.SshNet;
using Serilog;

namespace PhotonBypass.ServerBridge.Ssh;

class SshConnection(SshClient node, ServerEntity server, TimeSpan command_timeout) : ISshConnection
{
    public ServerEntity Server => server;

    public bool Execute(string command, out string result)
    {
        Log.Information("Execute command on server: {0}\n{1}", node.ConnectionInfo.Host, CommandScrubber.Scrub(command));

        var execution = node.CreateCommand(command);

        var operation = execution.BeginExecute();

        if (!operation.AsyncWaitHandle.WaitOne(command_timeout))
        {
            execution.CancelAsync();
            throw new UserException("خطای اجرای دستور در سرور!",
                $"Command timed out after {command_timeout.TotalSeconds:N0}s on server: {node.ConnectionInfo.Host}");
        }

        result = execution.EndExecute(operation);

        Log.Debug("Result command on server: {0} (length: {1})",
            node.ConnectionInfo.Host, (result ?? string.Empty).Length);

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
