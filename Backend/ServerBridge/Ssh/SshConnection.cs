using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.ErrorHandler;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.ServerBridge.Tunnel;
using Serilog;

namespace PhotonBypass.ServerBridge.Ssh;

/// <summary>
/// Command connection of one server. <see cref="Execute"/> returns false for a
/// failed command (stderr, a non-zero exit status, a RouterOS error text) and
/// reserves exceptions for a timeout and a lost connection (Docs/25 D10).
/// </summary>
class SshConnection : ISshConnection
{
    private const int LoggedResultLimit = 2000;

    private readonly ISshCommandChannel channel;

    private readonly TimeSpan command_timeout;

    private readonly Action on_dispose;

    /// <summary>Told when the session under the connection is gone — the tunnel pool drops exactly that session.</summary>
    private readonly Action<Exception>? on_transport_failure;

    private int disposed;

    internal SshConnection(
        ISshCommandChannel channel, ServerEntity server, TimeSpan commandTimeout, Action on_dispose,
        Action<Exception>? on_transport_failure = null)
    {
        this.channel = channel;
        Server = server;
        command_timeout = commandTimeout;
        this.on_dispose = on_dispose;
        this.on_transport_failure = on_transport_failure;
    }

    public ServerEntity Server { get; }

    public bool Execute(string command, out string result)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed) != 0, this);

        var host = channel.Host;

        Log.Information("Execute command on server: {0}\n{1}", host, CommandScrubber.Scrub(command));

        SshCommandResult execution;

        try
        {
            execution = channel.Execute(command, command_timeout);
        }
        catch (TimeoutException)
        {
            throw new UserException(SshErrors.CommandTimeoutMessage,
                $"Command timed out after {command_timeout.TotalSeconds:N0}s on server: {host}");
        }
        catch (SshConnectException error)
        {
            on_transport_failure?.Invoke(error);

            throw new UserException(SshErrors.ConnectMessage,
                $"Cannot connect to server: {host} ({error.Step ?? "command"}) after {error.ElapsedMilliseconds} ms");
        }

        var verdict = CommandOutcome.Classify(execution, Server.OsType);

        if (verdict.Success)
        {
            Log.Debug("Result command on server: {0} (length: {1})", host, verdict.Result.Length);
        }
        else
        {
            // a failed command no longer throws: the caller only gets false, so the evidence goes to the log
            Log.Warning("Command failed on server: {0} (exit-status: {1})\n{2}",
                host, execution.ExitStatus, Truncate(CommandScrubber.Scrub(verdict.Result)));
        }

        result = verdict.Result;

        return verdict.Success;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;

        on_dispose();
    }

    private static string Truncate(string text) =>
        text.Length <= LoggedResultLimit ? text : text[..LoggedResultLimit] + "…";
}
