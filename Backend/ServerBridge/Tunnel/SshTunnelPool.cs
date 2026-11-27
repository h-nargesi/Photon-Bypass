using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.ErrorHandler;
using PhotonBypass.ServerBridge.Hosted;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.ServerBridge.Ssh;
using Serilog;

namespace PhotonBypass.ServerBridge.Tunnel;

/// <summary>
/// The pooled SSH sessions of the tunnel transport (Docs/25 D5). One session
/// per server, keyed by <see cref="ServerEntity.Id"/> and rebuilt when the SSH
/// leg of the row changes or the consumer reports a transport failure. A
/// session is never idle-locked for long: a sweep disconnects whatever has
/// not been used for <see cref="ServerBridgeOptions.SshTunnelIdleSeconds"/>.
/// </summary>
internal sealed class SshTunnelPool : ISshTunnelPool, IHostedService, IDisposable
{
    internal static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(60);

    private const string LogPrefix = "ssh-tunnel";

    private readonly ISshConnector connector;

    private readonly ServerBridgeOptions settings;

    private readonly TimeProvider time_provider;

    private readonly object gate = new();

    private readonly Dictionary<int, Entry> entries = [];

    private readonly StoppingToken stopping = new();

    private Task sweeper = Task.CompletedTask;

    private int disposed;

    public SshTunnelPool(ISshConnector connector, IOptions<ServerBridgeOptions> options, TimeProvider time_provider)
    {
        this.connector = connector;
        settings = options.Value;
        this.time_provider = time_provider;
    }

    public async ValueTask<ISshTunnelSession> GetAsync(ServerEntity server, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(server);

        if (server.Id <= 0)
        {
            throw new ArgumentException(
                $"The SSH tunnel pool keys its sessions by {nameof(ServerEntity.Id)}; server '{server.Name}' has id {server.Id} and cannot open a tunnel.",
                nameof(server));
        }

        // Config decrypts and parses on every access: read it once per call
        var configuration = server.Config ?? throw NoConfiguration(server);

        var endpoint = SshEndpointFactory.Create(server, configuration, HostKeyPolicy.Required, settings);

        var entry = EntryOf(server.Id, create: true)!;

        // connects of one server are serialized, so concurrent callers share a single connection
        await entry.Lock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            // marks the live session used under the entry lock: the sweep never evicts a session that was just handed out
            var live = entry.Use();

            if (live != null)
            {
                if (live.Fingerprint == endpoint.Fingerprint && live.IsConnected) return live;

                entry.Take(live)?.Dispose(live.Fingerprint == endpoint.Fingerprint ? "session lost" : "configuration changed");
            }

            var session = new Session(this, server, endpoint, entry.Generation + 1);

            await session.ConnectAsync(ct);

            entry.Generation = session.Generation;

            entry.Set(session);

            return session;
        }
        finally
        {
            entry.Lock.Release();
        }
    }

    public void Fault(int serverId, int generation, string reason) => EntryOf(serverId, create: false)?.Take(generation)?.Dispose(reason);

    /// <summary>Drops exactly this session — the report of the session itself (its error event, a failed command or forward).</summary>
    internal void Fault(Session session, string reason) =>
        EntryOf(session.ServerId, create: false)?.Take(session)?.Dispose(reason);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        sweeper = SweepLoopAsync(stopping.Token);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // the host may stop after its run loop already did: the second stop is a no-op
        if (!stopping.TryStop()) return;

        try
        {
            await sweeper;
        }
        catch (OperationCanceledException) when (stopping.IsStopped)
        {
        }

        DropAll("host stopping");
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;

        stopping.Dispose();

        DropAll("pool disposed");
    }

    /// <summary>Disconnects the sessions idle for longer than the configured idle lifetime; returns how many.</summary>
    internal int Sweep()
    {
        var now = time_provider.GetUtcNow();
        var idle_after = TimeSpan.FromSeconds(settings.SshTunnelIdleSeconds);

        List<Entry> snapshot;

        lock (gate)
        {
            snapshot = [.. entries.Values];
        }

        var swept = 0;

        foreach (var entry in snapshot)
        {
            // idle check and removal under the entry lock: a session used in between is not idle anymore
            if (entry.TakeIdle(now, idle_after) is not { } idle) continue;

            idle.Dispose($"idle for more than {idle_after.TotalSeconds:N0}s");

            swept++;
        }

        return swept;
    }

    private async Task SweepLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(SweepInterval, time_provider);

        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                try
                {
                    Sweep();
                }
                catch (Exception error)
                {
                    Log.Error(error, "{0} sweep failed", LogPrefix);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private Entry? EntryOf(int serverId, bool create)
    {
        lock (gate)
        {
            if (entries.TryGetValue(serverId, out var entry)) return entry;

            if (!create) return null;

            entries.Add(serverId, entry = new Entry());

            return entry;
        }
    }

    private void DropAll(string reason)
    {
        List<Entry> snapshot;

        lock (gate)
        {
            snapshot = [.. entries.Values];
        }

        foreach (var entry in snapshot)
        {
            entry.Take()?.Dispose(reason);
        }
    }

    private static Exception NoConfiguration(ServerEntity server) =>
        new($"The configuration is not set for server ({server.Id}:{server.Name})");

    /// <summary>
    /// The slot of one server. Its session is swapped only under the entry
    /// lock, so a fault, a rebuild and the idle sweep can never act on a
    /// session other than the one they decided about.
    /// </summary>
    private sealed class Entry
    {
        private readonly object sync = new();

        public readonly SemaphoreSlim Lock = new(1, 1);

        /// <summary>Connect counter of the server; only mutated under <see cref="Lock"/>.</summary>
        public int Generation;

        public Session? Session { get; private set; }

        /// <summary>The live session, marked used; null when there is none.</summary>
        public Session? Use()
        {
            lock (sync)
            {
                if (Session is not { } session) return null;

                session.Touch();

                return session;
            }
        }

        public void Set(Session session)
        {
            lock (sync)
            {
                Session = session;
            }
        }

        /// <summary>Takes the session of this generation; null when another one (or none) is in the slot.</summary>
        public Session? Take(int generation)
        {
            lock (sync)
            {
                return Session is { } session && session.Generation == generation ? Detach(session) : null;
            }
        }

        /// <summary>Takes exactly this session; null when it was already replaced.</summary>
        public Session? Take(Session expected)
        {
            lock (sync)
            {
                return ReferenceEquals(Session, expected) ? Detach(expected) : null;
            }
        }

        /// <summary>Takes the session when it has been idle for longer than the given lifetime.</summary>
        public Session? TakeIdle(DateTimeOffset now, TimeSpan idle_after)
        {
            lock (sync)
            {
                return Session is { } session && session.IsIdle(now, idle_after) ? Detach(session) : null;
            }
        }

        public Session? Take()
        {
            lock (sync)
            {
                return Session is { } session ? Detach(session) : null;
            }
        }

        private Session Detach(Session session)
        {
            Session = null;

            return session;
        }
    }

    internal sealed class Session : ISshTunnelSession
    {
        private readonly SshTunnelPool host;

        private readonly ISshConnector connector;

        private readonly TimeProvider time_provider;

        private readonly ServerEntity server;

        private readonly SshEndpoint endpoint;

        private readonly SemaphoreSlim command_lock = new(1, 1);

        private readonly object sync = new();

        private readonly Dictionary<int, int> forwards = [];

        private ISshChannel? channel;

        private ISshCommandChannel? commands;

        private long last_activity_ticks;

        private int disposed;

        public Session(SshTunnelPool host, ServerEntity server, SshEndpoint endpoint, int generation)
        {
            this.host = host;
            connector = host.connector;
            time_provider = host.time_provider;
            this.server = server;
            this.endpoint = endpoint;
            Generation = generation;
        }

        public int Generation { get; }

        public int ServerId => server.Id;

        public string Fingerprint => endpoint.Fingerprint;

        public bool IsConnected
        {
            get
            {
                lock (sync)
                {
                    return channel?.IsConnected == true;
                }
            }
        }

        public async Task ConnectAsync(CancellationToken ct)
        {
            var watch = Stopwatch.StartNew();

            ISshChannel node;

            try
            {
                node = await connector.ConnectAsync(endpoint, ct);
            }
            catch (SshConnectException error)
            {
                Log.Warning("{0} connect-failed server={1}:{2} failure={3} ms={4}",
                    LogPrefix, endpoint.Host, endpoint.Port, error.Failure, error.ElapsedMilliseconds);

                throw SshErrors.ToUserException(error);
            }

            ISshCommandChannel command_channel;

            try
            {
                command_channel = await node.OpenCommandChannelAsync(ct);

                node.ErrorOccurred += OnSessionError;

                lock (sync)
                {
                    channel = node;
                    commands = command_channel;
                }
            }
            catch
            {
                node.Dispose();

                throw;
            }

            Touch();

            Log.Information("{0} connected server={1}:{2} generation={3} ms={4} host-key={5}",
                LogPrefix, endpoint.Host, endpoint.Port, Generation, (int)watch.Elapsed.TotalMilliseconds, node.HostKey.Sha256);
        }

        public int ForwardedPort(int remotePort)
        {
            if (remotePort is < 1 or > IPEndPoint.MaxPort) throw new ArgumentOutOfRangeException(nameof(remotePort));

            lock (sync)
            {
                var node = LiveChannel();

                if (forwards.TryGetValue(remotePort, out var bound)) return bound;

                int started;

                try
                {
                    started = node.StartLocalForward(remotePort);
                }
                catch (Exception error)
                {
                    // a session that cannot forward is dropped, so the next use reconnects
                    host.Fault(this, $"port-forward failed ({error.GetType().Name})");

                    throw new UserException(SshErrors.ConnectMessage,
                        $"Cannot start the port-forward to 127.0.0.1:{remotePort} on {endpoint.Host}:{endpoint.Port}: {error.GetType().Name}");
                }

                forwards.Add(remotePort, started);

                Touch();

                return started;
            }
        }

        public async Task<ISshConnection> OpenCommandChannelAsync(TimeSpan commandTimeout, CancellationToken ct)
        {
            // a dropped session must not wait behind a command that is still running on it
            EnsureLive();

            if (!await command_lock.WaitAsync(commandTimeout, ct).ConfigureAwait(false))
            {
                throw new UserException(SshErrors.CommandTimeoutMessage,
                    $"Waited {commandTimeout.TotalSeconds:N0}s for the command channel of the tunnel session " +
                    $"(server={server.Id}:{server.Name}, generation={Generation}): another command is still running.");
            }

            try
            {
                lock (sync)
                {
                    var node_commands = LiveChannel().IsConnected ? commands : null;

                    if (node_commands == null) throw Lost();

                    Touch();

                    return new SshConnection(node_commands, server, commandTimeout, Release, OnCommandLostSession);
                }
            }
            catch
            {
                command_lock.Release();

                throw;
            }
        }

        public void Touch() => Volatile.Write(ref last_activity_ticks, time_provider.GetUtcNow().Ticks);

        public bool IsIdle(DateTimeOffset now, TimeSpan idle_after) =>
            now.Ticks - Volatile.Read(ref last_activity_ticks) >= idle_after.Ticks;

        public void Dispose(string reason)
        {
            ISshChannel? node;

            ISshCommandChannel? node_commands;

            lock (sync)
            {
                if (Volatile.Read(ref disposed) != 0) return;

                Volatile.Write(ref disposed, 1);

                node = channel;
                channel = null;
                node_commands = commands;
                commands = null;
            }

            if (node == null) return;

            node.ErrorOccurred -= OnSessionError;

            node_commands?.Dispose();

            node.Dispose();

            Log.Information("{0} disconnected server={1}:{2} generation={3} reason={4}",
                LogPrefix, endpoint.Host, endpoint.Port, Generation, reason);
        }

        private void EnsureLive()
        {
            lock (sync)
            {
                if (IsDropped()) throw Lost();
            }
        }

        private bool IsDropped() => Volatile.Read(ref disposed) != 0 || channel == null;

        private ISshChannel LiveChannel()
        {
            if (IsDropped()) throw Lost();

            return channel!;
        }

        private void Release()
        {
            lock (sync)
            {
                commands?.Cancel();
            }

            command_lock.Release();
        }

        private void OnCommandLostSession(Exception error) =>
            // the command path reports a lost session like the forward paths do (Docs/25 §4.2)
            host.Fault(this, $"command failed ({error.GetType().Name})");

        private void OnSessionError(Exception error)
        {
            // raised on the SSH.NET thread: disposing the session from inside its own event would block it;
            // the report names this session only, so a replacement built in the meantime stays
            var reason = $"session error ({error.GetType().Name})";

            Task.Run(() => host.Fault(this, reason));
        }

        private UserException Lost() => new(SshErrors.ConnectMessage,
            $"The SSH tunnel session of the server ({server.Id}:{server.Name}, generation={Generation}) is no longer connected.");
    }
}
