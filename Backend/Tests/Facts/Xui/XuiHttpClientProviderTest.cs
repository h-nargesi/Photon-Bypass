using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.Test.Mock.MockServerBridge;
using PhotonBypass.Xui;
using PhotonBypass.Xui.WebService;

namespace PhotonBypass.Test.Facts.Xui;

/// <summary>
/// The stable <see cref="HttpClient"/> per tunnel server (Docs/25 D7): one
/// instance across reconnects and generation changes, a handler without a
/// proxy whose connections ask the pool for the forwarded loopback port, and
/// a transport failure reported back to the pool. The forward is a loopback
/// <see cref="TcpListener"/>, never a real server.
/// </summary>
public class XuiHttpClientProviderTest
{
    private readonly FakeTunnelPool pool = new();

    [Fact]
    public void Get_Direct_CreatesTheNamedClientOnEveryCall()
    {
        using var factory = new RecordingFactory();
        using var provider = new XuiHttpClientProvider(factory, pool);

        using var first = provider.Get(TunnelFacts.Server(transport: ServerTransport.Direct));
        using var second = provider.Get(TunnelFacts.Server(transport: ServerTransport.Direct));

        factory.Names.Should().Equal("xui-panel", "xui-panel");
        first.Should().NotBeSameAs(second);
    }

    [Fact]
    public void Get_Tunnel_ReturnsOneClientThatSurvivesReconnectsAndGenerationChanges()
    {
        using var factory = new RecordingFactory();
        using var provider = new XuiHttpClientProvider(factory, pool);

        var first = provider.Get(TunnelFacts.Server());
        var second = provider.Get(TunnelFacts.Server());

        pool.Fault(7, 1, "tunnel died");
        pool.Generation++;

        var third = provider.Get(TunnelFacts.Server());

        second.Should().BeSameAs(first);
        third.Should().BeSameAs(first, "only the connections consult the pool again; the client is never rebuilt");
        pool.Asked.Should().BeEmpty("a client is handed out without touching the pool");
    }

    [Theory]
    [InlineData(2087, false, null)]
    [InlineData(2053, true, null)]
    [InlineData(2053, false, "xui84")]
    public void Get_Tunnel_AChangedKeyMeansAnotherClient(int port, bool ssl, string? base_path)
    {
        using var provider = new XuiHttpClientProvider(new RecordingFactory(), pool);

        var original = provider.Get(TunnelFacts.Server(web: Web(port: 2053)));
        var changed = provider.Get(TunnelFacts.Server(web: Web(port: port, ssl: ssl, base_path: base_path)));

        changed.Should().NotBeSameAs(original);
        provider.Get(TunnelFacts.Server(web: Web(port: port, ssl: ssl, base_path: base_path))).Should().BeSameAs(changed);
    }

    [Fact]
    public void TunnelHandler_NeverUsesAProxy_AndPoolsConnectionsBriefly()
    {
        using var provider = new XuiHttpClientProvider(new RecordingFactory(), pool);

        using var handler = provider.CreateTunnelHandler(() => TunnelFacts.Server());

        handler.UseProxy.Should().BeFalse("the destination is always the loopback forward");
        handler.PooledConnectionLifetime.Should().Be(TimeSpan.FromMinutes(5));
        handler.PooledConnectionIdleTimeout.Should().Be(TimeSpan.FromMinutes(1));
        handler.ConnectCallback.Should().NotBeNull();
    }

    [Fact]
    public async Task TunnelClient_MirrorsTheNamedClientsDefaults()
    {
        using var factory = new RecordingFactory();
        using var provider = new XuiHttpClientProvider(factory, pool);

        using var client = provider.Get(TunnelFacts.Server());

        client.Timeout.Should().Be(TimeSpan.FromSeconds(77));
        client.DefaultRequestHeaders.Accept.Should().Contain(header => header.MediaType == "application/json");
    }

    [Fact]
    public async Task ConnectCallback_ConnectsThroughTheForwardedLoopbackPort()
    {
        using var factory = new RecordingFactory();
        using var provider = new XuiHttpClientProvider(factory, pool);
        using var listener = new TcpListener(IPAddress.Loopback, 0);

        listener.Start();

        var server = TunnelFacts.Server(web: Web(port: 2053));

        pool.LocalPorts[2053] = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var client = provider.Get(server);

        var responder = RespondAsync(listener, """{"success":true,"msg":"","obj":null}""");

        // the URL carries the panel's address on the server's own loopback; the TCP connection goes to the forward
        var body = await client.GetStringAsync("http://127.0.0.1:2053/xui/login");

        await responder;

        body.Should().Contain("\"success\":true");
        pool.Asked.Should().ContainSingle().Which.Should().BeSameAs(server);
    }

    [Fact]
    public async Task ConnectCallback_UsesTheLatestRowOfTheKey_NotAStaleOne()
    {
        using var provider = new XuiHttpClientProvider(new RecordingFactory(), pool);
        using var listener = new TcpListener(IPAddress.Loopback, 0);

        listener.Start();

        var stale = TunnelFacts.Server();
        var latest = TunnelFacts.Server(key: TunnelFacts.OtherKeyText);

        pool.LocalPorts[2053] = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var client = provider.Get(stale);

        provider.Get(latest);

        var responder = RespondAsync(listener, """{"success":true,"msg":"","obj":null}""");

        await client.GetStringAsync("http://127.0.0.1:2053/xui/login");

        await responder;

        pool.Asked.Should().ContainSingle().Which.Should().BeSameAs(latest,
            "a new connection after a key change must rebuild the pooled session from the current row");
    }

    [Fact]
    public async Task TunnelClient_ClosedForwardedPort_FailsOnTheWire_AndFaultsTheSession()
    {
        using var provider = new XuiHttpClientProvider(new RecordingFactory(), pool);
        using var listener = new TcpListener(IPAddress.Loopback, 0);

        listener.Start();
        listener.Stop();

        pool.LocalPorts[2053] = ((IPEndPoint)listener.LocalEndpoint).Port;

        using var client = provider.Get(TunnelFacts.Server(web: Web(port: 2053)));

        var error = await Assert.ThrowsAnyAsync<Exception>(() => client.GetStringAsync("http://127.0.0.1:2053/xui/login"));

        XuiTransportDeath.Is(error).Should().BeTrue("a listener that is gone is a transport death, not an answer");
        pool.Faults.Should().ContainSingle().Which.Should().Be((7, 1, "x-ui connect through the tunnel failed (SocketException)"));
    }

    [Fact]
    public async Task ReportTransportFailure_FaultsTheGenerationTheProvidersConnectionsLastUsed()
    {
        using var provider = new XuiHttpClientProvider(new RecordingFactory(), pool);
        using var listener = new TcpListener(IPAddress.Loopback, 0);

        listener.Start();

        pool.LocalPorts[2053] = ((IPEndPoint)listener.LocalEndpoint).Port;

        var server = TunnelFacts.Server(web: Web(port: 2053));

        using (var client = provider.Get(server))
        {
            var responder = RespondAsync(listener, """{"success":true,"msg":"","obj":null}""");

            await client.GetStringAsync("http://127.0.0.1:2053/xui/login");

            await responder;
        }

        // another consumer reconnected in the meantime: the server's session is now generation 2
        pool.Generation = 2;

        provider.ReportTransportFailure(server);
        provider.ReportTransportFailure(TunnelFacts.Server(transport: ServerTransport.Direct));
        provider.ReportTransportFailure(TunnelFacts.Server(id: 8));

        // the generation this provider's connections used goes to the pool, which leaves any newer session alone
        pool.Faults.Should().ContainSingle().Which.Should().Be((7, 1, "x-ui transport failure"));
    }

    [Fact]
    public void ReportTransportFailure_ForATunnelServerNeverConnected_FaultsNothing()
    {
        using var provider = new XuiHttpClientProvider(new RecordingFactory(), pool);

        provider.ReportTransportFailure(TunnelFacts.Server());

        pool.Faults.Should().BeEmpty("there is no session this provider's connections could have failed on");
    }

    [Fact]
    public async Task Dispose_ReleasesTheTunnelClients_AndRefusesNewOnes()
    {
        var provider = new XuiHttpClientProvider(new RecordingFactory(), pool);

        var client = provider.Get(TunnelFacts.Server());

        provider.Dispose();

        var act = () => client.GetStringAsync("http://127.0.0.1:2053/");

        await act.Should().ThrowAsync<ObjectDisposedException>();
        provider.Invoking(p => p.Get(TunnelFacts.Server())).Should().Throw<ObjectDisposedException>();
    }

    private static WebApiConfig Web(int port, bool ssl = false, string? base_path = null) => new()
    {
        HostName = "127.0.0.1",
        Port = port,
        Ssl = ssl,
        BasePath = base_path,
        Username = "admin",
        Password = "api-secret",
    };

    private static int ClosedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);

        listener.Start();
        listener.Stop();

        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    /// <summary>Answers the next loopback connection with one minimal HTTP/1.1 response.</summary>
    private static async Task RespondAsync(TcpListener listener, string body)
    {
        using var client = await listener.AcceptTcpClientAsync();

        var stream = client.GetStream();

        await stream.ReadAsync(new byte[8192]);

        var payload = Encoding.UTF8.GetBytes(body);

        await stream.WriteAsync(Encoding.ASCII.GetBytes(
            $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n"));
        await stream.WriteAsync(payload);
        await stream.FlushAsync();
    }

    /// <summary>The named <c>xui-panel</c> client with recognizable defaults.</summary>
    private sealed class RecordingFactory : IHttpClientFactory, IDisposable
    {
        private readonly HttpClientHandler handler = new();

        public List<string> Names { get; } = [];

        public HttpClient CreateClient(string name)
        {
            Names.Add(name);

            var client = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(77) };

            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            return client;
        }

        public void Dispose() => handler.Dispose();
    }
}
