using System.Net;
using System.Net.Sockets;
using Renci.SshNet;
using Renci.SshNet.Common;
using Serilog;

namespace PhotonBypass.ServerBridge.Tunnel;

/// <summary>
/// One SSH.NET session. Command channels are serialized by the owner (the
/// direct handler or the tunnel pool), never here; every port-forward
/// listens on the loopback address only (Docs/25 G2).
/// </summary>
internal sealed class SshNetChannel : ISshChannel
{
    private readonly SshClient? client;

    private readonly SshEndpoint? endpoint;

    private readonly EventHandler<ExceptionEventArgs> on_client_error;

    private readonly object gate = new();

    private readonly Dictionary<int, ForwardedPortLocal> forwards = [];

    private bool disposed;

    private SshNetChannel(SshClient? client, SshEndpoint? endpoint, string host, HostKeyCheck host_key)
    {
        this.client = client;
        this.endpoint = endpoint;
        Host = host;
        HostKey = host_key;

        on_client_error = (_, args) => ErrorOccurred?.Invoke(args.Exception);

        if (client != null) client.ErrorOccurred += on_client_error;
    }

    public SshNetChannel(SshClient client, SshEndpoint endpoint, HostKeyCheck host_key)
        : this(client, endpoint, endpoint.Host, host_key)
    {
    }

    public string Host { get; }

    public bool IsConnected => client?.IsConnected == true;

    public HostKeyCheck HostKey { get; }

    public event Action<Exception>? ErrorOccurred;

    /// <summary>The channel of a discovery connect: it holds the fingerprint and is already disconnected.</summary>
    public static ISshChannel Discovered(HostKeyCheck host_key) => new SshNetChannel(null, null, string.Empty, host_key);

    public int StartLocalForward(int remotePort)
    {
        if (remotePort is < 1 or > IPEndPoint.MaxPort)
            throw new ArgumentOutOfRangeException(nameof(remotePort));

        lock (gate)
        {
            ThrowIfDisposed();

            var node = client ?? throw NotConnected();

            if (forwards.TryGetValue(remotePort, out var existing)) return (int)existing.BoundPort;

            var forward = new ForwardedPortLocal("127.0.0.1", 0, "127.0.0.1", (uint)remotePort);

            // a refused channel (authorized_keys permitopen, RouterOS forwarding-enabled) is reported by the service call, not here
            forward.Exception += (_, error) => Log.Debug(
                "Forwarded connection to 127.0.0.1:{0} over {1} failed: {2}", remotePort, Host, error.GetType().Name);

            node.AddForwardedPort(forward);

            try
            {
                forward.Start();
            }
            catch
            {
                node.RemoveForwardedPort(forward);

                throw;
            }

            forwards.Add(remotePort, forward);

            return (int)forward.BoundPort;
        }
    }

    public Task<ISshCommandChannel> OpenCommandChannelAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        lock (gate)
        {
            ThrowIfDisposed();

            var node = client ?? throw NotConnected();

            return Task.FromResult<ISshCommandChannel>(new SshNetCommandChannel(node, endpoint!, Host));
        }
    }

    public void Disconnect()
    {
        lock (gate)
        {
            if (disposed || client == null) return;

            Close(client);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;

            disposed = true;

            if (client == null) return;

            client.ErrorOccurred -= on_client_error;

            Close(client);

            client.Dispose();
        }
    }

    private void Close(SshClient node)
    {
        foreach (var forward in forwards.Values)
        {
            try
            {
                if (forward.IsStarted) forward.Stop();
            }
            catch (Exception error)
            {
                Log.Debug("Stopping a forwarded port on {0} failed: {1}", Host, error.GetType().Name);
            }

            node.RemoveForwardedPort(forward);
        }

        forwards.Clear();

        try
        {
            node.Disconnect();
        }
        catch (Exception error)
        {
            Log.Debug("SSH disconnect on {0} failed: {1}", Host, error.GetType().Name);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private static InvalidOperationException NotConnected() =>
        new("The SSH channel is not connected.");
}

internal sealed class SshNetCommandChannel(SshClient client, SshEndpoint endpoint, string host) : ISshCommandChannel
{
    private const int NoExitStatus = -1;

    // written by the executing thread, read by the one that cancels
    private volatile SshCommand? running;

    public string Host { get; } = host;

    public SshCommandResult Execute(string command, TimeSpan timeout)
    {
        // the command owns its channel: a timed-out one is closed here instead of waiting for the server
        using var execution = client.CreateCommand(command);

        running = execution;

        try
        {
            var operation = execution.BeginExecute();

            if (!operation.AsyncWaitHandle.WaitOne(timeout))
            {
                execution.CancelAsync();

                throw new TimeoutException($"Command timed out after {timeout.TotalSeconds:N0}s on server: {Host}");
            }

            return new SshCommandResult(
                execution.ExitStatus ?? NoExitStatus,
                execution.EndExecute(operation) ?? string.Empty,
                execution.Error ?? string.Empty);
        }
        catch (Exception error) when (error is SshConnectionException or SocketException or ObjectDisposedException)
        {
            throw new SshConnectException(SshConnectFailure.Connect, endpoint, step: "command", inner_exception: error);
        }
        finally
        {
            running = null;
        }
    }

    public void Cancel()
    {
        running?.CancelAsync();
    }

    public void Dispose()
    {
        Cancel();
    }
}
