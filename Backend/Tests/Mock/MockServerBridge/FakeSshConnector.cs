using System.Text.Json;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.ServerBridge;
using PhotonBypass.ServerBridge.Services;
using OperatingSystem = PhotonBypass.Domain.Servers.Types.OperatingSystem;
using PhotonBypass.ServerBridge.Tunnel;

namespace PhotonBypass.Test.Mock.MockServerBridge;

/// <summary>
/// Fakes of the SSH seam (Docs/25 D3–D5): no network, every call recorded.
/// They replace <c>ISshConnector</c>, the channel and its command channel.
/// </summary>
internal sealed class FakeSshConnector : ISshConnector
{
    public List<SshEndpoint> Endpoints { get; } = [];

    /// <summary>Decides what a connect returns or throws; a null result yields a connected <see cref="FakeSshChannel"/>.</summary>
    public Func<SshEndpoint, ISshChannel?>? OnConnect { get; set; }

    public List<FakeSshChannel> Channels { get; } = [];

    public FakeSshChannel? LastChannel => Channels.LastOrDefault();

    public int Connects => Endpoints.Count;

    public Task<ISshChannel> ConnectAsync(SshEndpoint endpoint, CancellationToken ct)
    {
        Endpoints.Add(endpoint);

        var channel = OnConnect?.Invoke(endpoint) ?? new FakeSshChannel();

        if (channel is FakeSshChannel fake) Channels.Add(fake);

        return Task.FromResult(channel);
    }
}

internal sealed class FakeSshChannel : ISshChannel
{
    private int next_port = 40_000;

    public FakeSshChannel()
    {
        CommandChannel = new FakeSshCommandChannel(this);
    }

    public bool IsConnected { get; set; } = true;

    public HostKeyCheck HostKey { get; set; } = new(TunnelFacts.PinnedKey, "ssh-ed25519", HostKeyStatus.Trusted);

    public event Action<Exception>? ErrorOccurred;

    public FakeSshCommandChannel CommandChannel { get; }

    public Dictionary<int, int> Forwards { get; } = [];

    public bool Disconnected { get; private set; }

    /// <summary>Thrown by <see cref="StartLocalForward"/>: a session that cannot forward.</summary>
    public Exception? ForwardFailure { get; set; }

    public int DisposeCalls { get; private set; }

    public int DisconnectCalls { get; private set; }

    public int StartForwardCalls { get; private set; }

    public int NextForwardPort
    {
        get => next_port;
        set => next_port = value;
    }

    public int StartLocalForward(int remotePort)
    {
        ObjectDisposedException.ThrowIf(Disconnected, this);

        StartForwardCalls++;

        if (ForwardFailure != null) throw ForwardFailure;

        int bound;

        lock (Forwards)
        {
            if (!Forwards.TryGetValue(remotePort, out bound)) Forwards[remotePort] = bound = next_port++;
        }

        return bound;
    }

    public Task<ISshCommandChannel> OpenCommandChannelAsync(CancellationToken ct) => Task.FromResult<ISshCommandChannel>(CommandChannel);

    public void Disconnect()
    {
        DisconnectCalls++;
        Disconnected = true;
        IsConnected = false;
    }

    public void Dispose()
    {
        DisposeCalls++;
        Disconnected = true;
        IsConnected = false;
    }

    public void RaiseSessionError(Exception error) => ErrorOccurred?.Invoke(error);
}

internal sealed class FakeSshCommandChannel(FakeSshChannel owner) : ISshCommandChannel
{
    public Queue<SshCommandResult> Results { get; } = [];

    /// <summary>Overrides <see cref="Results"/>: may block, throw, or return a result.</summary>
    public Func<string, Task<SshCommandResult>>? OnExecute { get; set; }

    public List<string> Commands { get; } = [];

    public List<TimeSpan> Timeouts { get; } = [];

    public int CancelCalls { get; private set; }

    public string Host => "10.0.0.1";

    public SshCommandResult Execute(string command, TimeSpan timeout)
    {
        if (owner.Disconnected) throw new ObjectDisposedException(nameof(FakeSshChannel));

        Commands.Add(command);
        Timeouts.Add(timeout);

        if (OnExecute != null) return OnExecute(command).GetAwaiter().GetResult();

        return Results.Count > 0 ? Results.Dequeue() : new SshCommandResult(0, string.Empty, string.Empty);
    }

    public void Cancel() => CancelCalls++;

    public void Dispose()
    {
    }
}

/// <summary>
/// Fake of the pooled tunnel sessions: records every asked entity and
/// fault, and maps each remote port to a distinct loopback port.
/// </summary>
internal sealed class FakeTunnelPool : ISshTunnelPool
{
    public List<ServerEntity> Asked { get; } = [];

    public List<(int ServerId, int Generation, string Reason)> Faults { get; } = [];

    public int Generation { get; set; } = 1;

    /// <summary>Remote-port overrides of the mapped loopback port (default: remote + 30_000).</summary>
    public Dictionary<int, int> LocalPorts { get; } = [];

    public ValueTask<ISshTunnelSession> GetAsync(ServerEntity server, CancellationToken ct = default)
    {
        Asked.Add(server);

        return ValueTask.FromResult<ISshTunnelSession>(new Session(this));
    }

    public void Fault(int serverId, int generation, string reason) => Faults.Add((serverId, generation, reason));

    private sealed class Session(FakeTunnelPool owner) : ISshTunnelSession
    {
        public int Generation => owner.Generation;

        public int ForwardedPort(int remotePort) =>
            owner.LocalPorts.TryGetValue(remotePort, out var local) ? local : remotePort + 30_000;

        public Task<ISshConnection> OpenCommandChannelAsync(TimeSpan commandTimeout, CancellationToken ct = default) =>
            throw new NotSupportedException("the command path is covered through SshHandlerTest");
    }
}

internal static class TunnelFacts
{
    public const string KeyText =
        "-----BEGIN OPENSSH PRIVATE KEY-----\nb3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAABFwAAAAdzc2gtZW\nQ255MTkAAAAA\n-----END OPENSSH PRIVATE KEY-----";

    public const string PassPhrase = "pass-phrase-secret";

    public const string Password = "ssh-password-secret";

    public const string OtherKeyText =
        "-----BEGIN OPENSSH PRIVATE KEY-----\nb3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAABFwAAAAdzc2gtZW\nQ255MTkBBBBB\n-----END OPENSSH PRIVATE KEY-----";

    public const string PinnedKey = "SHA256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    public const string PresentedKey = "SHA256:BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB";

    public static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);

    public static readonly TimeSpan KeepAlive = TimeSpan.FromSeconds(30);

    public static readonly ServerBridgeOptions DefaultOptions = new();

    /// <summary>
    /// A persisted server row in plaintext JSON (the protection delegates are
    /// swapped by parallel test classes, so the Config property is not used).
    /// </summary>
    public static ServerEntity Server(
        int id = 7, string ip = "10.0.0.7", ServerTransport transport = ServerTransport.SshTunnel,
        ServerFeature features = ServerFeature.Xui | ServerFeature.V2ray, int ssh_port = 22,
        string key = KeyText, string pin = PinnedKey, string username = "panel",
        string password = Password, string? passphrase = PassPhrase,
        WebApiConfig? web = null)
    {
        return new ServerEntity
        {
            Id = id,
            Name = "T" + id,
            IpAddress = ip,
            DomainName = "t" + id,
            OsType = OperatingSystem.Ubuntu,
            Features = features,
            JsonConfig = JsonSerializer.Serialize(new ServerConfiguration
            {
                Transport = transport,
                SshConfig = new SshConfig
                {
                    Username = username,
                    Password = password,
                    Port = ssh_port,
                    PrivateKey = key,
                    PassPhrase = passphrase,
                    HostKeySha256 = pin,
                },
                WebApiConfig = web ?? new WebApiConfig
                {
                    HostName = transport == ServerTransport.SshTunnel ? "127.0.0.1" : ip,
                    Port = 2053,
                    Username = "admin",
                    Password = "api-secret",
                },
            }),
        };
    }
}
