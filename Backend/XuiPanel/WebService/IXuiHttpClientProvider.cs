using System.Net;
using System.Net.Sockets;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.ServerBridge.Tunnel;

namespace PhotonBypass.Xui.WebService;

/// <summary>
/// The <see cref="HttpClient"/> the x-ui adapter talks a server with (Docs/25 D7).
/// The direct transport gets the named client; the tunnel transport gets one
/// stable client per server whose connections ride the pooled SSH session.
/// </summary>
internal interface IXuiHttpClientProvider
{
    HttpClient Get(ServerEntity server);

    /// <summary>Drops the pooled tunnel session after a transport failure; does nothing on the direct transport.</summary>
    void ReportTransportFailure(ServerEntity server);
}

internal sealed class XuiHttpClientProvider(IHttpClientFactory factory, ISshTunnelPool pool) : IXuiHttpClientProvider, IDisposable
{
    private const int DefaultApiPort = 2053;

    private static readonly TimeSpan PooledConnectionLifetime = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1);

    private readonly object gate = new();

    private readonly Dictionary<ClientKey, Entry> clients = [];

    private bool disposed;

    public HttpClient Get(ServerEntity server)
    {
        // Config decrypts and parses on every access: read it once per call
        var configuration = server.Config;

        if (configuration?.Transport != ServerTransport.SshTunnel)
        {
            return factory.CreateClient(ServiceFactory.HttpClientKeyName);
        }

        var config = configuration.WebApiConfig ??
                     throw new Exception($"The web-api configuration is not set for server ({server.Id}:{server.Name})");

        var key = KeyOf(server, config);

        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);

            if (clients.TryGetValue(key, out var entry))
            {
                // the latest row decides where the next pooled connection goes
                entry.Server = server;

                return entry.Client;
            }

            Entry? created = null;

            // the client is never rebuilt on a reconnect or a generation change:
            // only its connections consult the pool again
            var client = CreateOwned(CreateTunnelHandler(() => created!.Server, generation => created!.Generation = generation));

            created = new Entry(client) { Server = server };

            clients.Add(key, created);

            return client;
        }
    }

    public void ReportTransportFailure(ServerEntity server)
    {
        // Config decrypts and parses on every access: read it once per call
        var configuration = server.Config;

        if (configuration?.Transport != ServerTransport.SshTunnel) return;

        var config = configuration.WebApiConfig;
        var entry = config == null ? null : EntryOf(KeyOf(server, config));

        // only the session this provider's connections last used is dropped; a replacement stays
        if (entry?.Generation is { } generation) pool.Fault(server.Id, generation, "x-ui transport failure");
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;

            disposed = true;

            foreach (var entry in clients.Values)
            {
                entry.Client.Dispose();
            }

            clients.Clear();
        }
    }

    /// <summary>
    /// A client that owns its handler but otherwise mirrors the defaults of
    /// the named <c>xui-panel</c> client (timeout, default headers).
    /// </summary>
    internal HttpClient CreateOwned(HttpMessageHandler handler)
    {
        var template = factory.CreateClient(ServiceFactory.HttpClientKeyName);

        var client = new HttpClient(handler, disposeHandler: true) { Timeout = template.Timeout };

        foreach (var header in template.DefaultRequestHeaders)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }

        return client;
    }

    /// <summary>
    /// The tunnel handler: no proxy (the destination is always loopback) and
    /// connections that ask the pool for the server's forwarded port.
    /// </summary>
    /// <param name="remember_generation">
    /// Receives the generation of every session a connection goes through, so a
    /// later transport failure drops that session and no other.
    /// </param>
    internal SocketsHttpHandler CreateTunnelHandler(Func<ServerEntity> current, Action<int>? remember_generation = null)
    {
        return new SocketsHttpHandler
        {
            UseProxy = false,
            PooledConnectionLifetime = PooledConnectionLifetime,
            PooledConnectionIdleTimeout = PooledConnectionIdleTimeout,
            ConnectCallback = (_, ct) => ConnectThroughTunnelAsync(current(), remember_generation, ct),
        };
    }

    private async ValueTask<Stream> ConnectThroughTunnelAsync(
        ServerEntity server, Action<int>? remember_generation, CancellationToken ct)
    {
        ISshTunnelSession? session = null;

        try
        {
            var config = server.Config?.WebApiConfig ??
                         throw new Exception($"The web-api configuration is not set for server ({server.Id}:{server.Name})");

            session = await pool.GetAsync(server, ct);

            remember_generation?.Invoke(session.Generation);

            var local_port = session.ForwardedPort(config.Port ?? DefaultApiPort);

            var tcp = new TcpClient();

            try
            {
                await tcp.ConnectAsync(IPAddress.Loopback, local_port, ct);
            }
            catch
            {
                tcp.Dispose();

                throw;
            }

            return tcp.GetStream();
        }
        catch (Exception error) when (error is SocketException or IOException or ObjectDisposedException)
        {
            // the loopback listener of the session is gone: drop it so the next connection reconnects
            if (session != null)
            {
                pool.Fault(server.Id, session.Generation, $"x-ui connect through the tunnel failed ({error.GetType().Name})");
            }

            throw;
        }
    }

    private Entry? EntryOf(ClientKey key)
    {
        lock (gate)
        {
            return clients.TryGetValue(key, out var entry) ? entry : null;
        }
    }

    private static ClientKey KeyOf(ServerEntity server, WebApiConfig config) =>
        new(server.Id, config.Port ?? 0, config.Ssl, config.BasePath);

    private readonly record struct ClientKey(int ServerId, int Port, bool Ssl, string? BasePath);

    private sealed class Entry(HttpClient client)
    {
        private volatile int generation;

        public HttpClient Client { get; } = client;

        /// <summary>The latest row seen for this key: the next pooled connection asks the pool with it.</summary>
        public volatile ServerEntity Server = null!;

        /// <summary>Generation of the session this key's connections last used; zero before the first one.</summary>
        public int Generation
        {
            get => generation;
            set => generation = value;
        }
    }
}
