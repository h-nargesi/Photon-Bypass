using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Domain.Servers.Model;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.Test.Mock.MockServerBridge;
using PhotonBypass.Xui;
using PhotonBypass.Xui.WebService;

namespace PhotonBypass.Test.Facts.Xui;

/// <summary>
/// The service step of the connection test for x-ui (Docs/25 D9): a real
/// login through the adapter's contract, on loopback listeners only — the
/// named client on the direct transport, the test's own forward on the
/// tunnel transport.
/// </summary>
public class XuiServiceProbeTest
{
    private const string LoginOk = """{"success":true,"msg":"","obj":null}""";

    private readonly FakeTunnelPool pool = new();

    [Fact]
    public async Task Direct_LogsInThroughTheNamedClient()
    {
        var handler = ScriptedHandler.Responding(LoginOk);

        var step = await Probe(handler).ProbeAsync(Server(), null);

        step.Step.Should().Be(ConnectionStepModel.Service);
        step.Passed.Should().BeTrue();
        step.Detail.Should().Contain("x-ui");
        handler.Requests.Single().AbsoluteUri.Should().Be("http://panel.local:5443/xui84/login");
        handler.Factory.Names.Should().Equal(ServiceFactory.HttpClientKeyName);
    }

    [Fact]
    public async Task Tunnel_LogsInThroughTheTestsOwnForward_NeverThePool()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);

        listener.Start();

        var responder = RespondAsync(listener, LoginOk, "x-ui=t1; Path=/;");

        var step = await Probe(ScriptedHandler.Responding(LoginOk))
            .ProbeAsync(Server(transport: ServerTransport.SshTunnel, host: "127.0.0.1", port: 2087),
                ((IPEndPoint)listener.LocalEndpoint).Port);

        await responder;

        step.Passed.Should().BeTrue();
        pool.Asked.Should().BeEmpty("the pooled session belongs to the adapter, not to the test");
    }

    [Theory]
    [InlineData(ServerTransport.Direct, "ارتباط با پنل x-ui برقرار نشد!")]
    [InlineData(ServerTransport.SshTunnel, "ورود به پنل x-ui ممکن نشد!")]
    public async Task TransportFailure_IsReportedWithItsTransportSpecificWording(ServerTransport transport, string expected)
    {
        var handler = ScriptedHandler.Responding(LoginOk);

        handler.ThrowAt(0, new HttpRequestException("An error occurred while sending the request", new IOException("closed")));

        // on the direct transport the failure comes from the named client; on the tunnel from a gone forward
        handler.ThrowAt(0, new HttpRequestException("An error occurred while sending the request", new IOException("closed")));

        var step = await Probe(handler).ProbeAsync(
            Server(transport: transport, host: "127.0.0.1"), transport == ServerTransport.Direct ? null : ClosedPort());

        step.Passed.Should().BeFalse();
        step.Detail.Should().StartWith(expected);
    }

    [Fact]
    public async Task Tunnel_TransportFailure_NamesBothCauses()
    {
        var handler = ScriptedHandler.Responding(LoginOk);

        handler.ThrowAt(0, new HttpRequestException("An error occurred while sending the request", new IOException("closed")));

        var step = await Probe(handler).ProbeAsync(Server(transport: ServerTransport.SshTunnel, host: "127.0.0.1"), ClosedPort());

        step.Detail.Should().Contain("عبور ترافیک از تانل").And.Contain("سرویس مقصد ارتباط را بست");
    }

    [Fact]
    public async Task RejectedCredentials_AreNotATransportFailure()
    {
        var handler = ScriptedHandler.Responding("""{"success":false,"msg":"wrong credentials","obj":null}""");

        var step = await Probe(handler).ProbeAsync(Server(), null);

        step.Passed.Should().BeFalse();
        step.Detail.Should().Be("اعتبارنامه‌ی پنل x-ui رد شد!");
    }

    [Fact]
    public async Task Timeout_IsNotATransportFailure()
    {
        var handler = ScriptedHandler.Responding(LoginOk);

        handler.ThrowAt(0, new TaskCanceledException("canceled", new TimeoutException()));

        var step = await Probe(handler).ProbeAsync(Server(), null);

        step.Detail.Should().Be("پاسخی از پنل x-ui دریافت نشد!");
    }

    [Fact]
    public async Task Tunnel_RejectedCredentials_AreNotBlamedOnTheForward()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);

        listener.Start();

        var responder = RespondAsync(listener, """{"success":false,"msg":"wrong credentials","obj":null}""", null);

        var step = await Probe(ScriptedHandler.Responding(LoginOk))
            .ProbeAsync(Server(transport: ServerTransport.SshTunnel, host: "127.0.0.1"), ((IPEndPoint)listener.LocalEndpoint).Port);

        await responder;

        step.Detail.Should().Be("اعتبارنامه‌ی پنل x-ui رد شد!");
    }

    [Fact]
    public async Task WithoutAWebConfiguration_FailsWithoutAnyRequest()
    {
        var handler = ScriptedHandler.Responding(LoginOk);

        var server = TunnelFacts.Server();
        server.JsonConfig = """{"Transport":"SshTunnel","SshConfig":{"Username":"panel","Port":22}}""";

        var step = await Probe(handler).ProbeAsync(server, null);

        step.Passed.Should().BeFalse();
        step.Detail.Should().Contain("پیکربندی وب");
        handler.Requests.Should().BeEmpty();
    }

    private XuiServiceProbe Probe(ScriptedHandler handler)
    {
        return new XuiServiceProbe(new XuiHttpClientProvider(handler.Factory, pool));
    }

    private static ServerEntity Server(ServerTransport transport = ServerTransport.Direct, string host = "panel.local", int port = 5443)
    {
        return TunnelFacts.Server(
            transport: transport,
            web: new WebApiConfig { HostName = host, Port = port, BasePath = "xui84", Username = "admin", Password = "api-secret" });
    }

    private static int ClosedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);

        listener.Start();
        listener.Stop();

        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static async Task RespondAsync(TcpListener listener, string body, string? cookie)
    {
        using var client = await listener.AcceptTcpClientAsync();

        var stream = client.GetStream();

        await stream.ReadAsync(new byte[8192]);

        var payload = Encoding.UTF8.GetBytes(body);

        var headers = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n";

        if (cookie != null) headers += $"Set-Cookie: {cookie}\r\n";

        await stream.WriteAsync(Encoding.ASCII.GetBytes(headers + "\r\n"));
        await stream.WriteAsync(payload);
        await stream.FlushAsync();
    }

    private sealed class ScriptedHandler(HttpMessageHandler handler)
    {
        public RecordingFactory Factory { get; } = new(handler);

        public static ScriptedHandler Responding(string body) => new(new BodyHandler(body));

        public List<Uri> Requests => Factory.Requests;

        public void ThrowAt(int request_index, Exception error) => Factory.ThrowAt(request_index, error);
    }

    private sealed class BodyHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };

            response.Headers.Add("Set-Cookie", "x-ui=t1; Path=/");

            return Task.FromResult(response);
        }
    }

    /// <summary>The named <c>xui-panel</c> client on a scripted handler that records requests and can die on the wire.</summary>
    private sealed class RecordingFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public List<string> Names { get; } = [];

        public List<Uri> Requests { get; } = [];

        private Dictionary<int, Exception> Throws { get; } = [];

        public HttpClient CreateClient(string name)
        {
            Names.Add(name);

            return new HttpClient(new RecordingHandler(this) { InnerHandler = handler }, disposeHandler: true);
        }

        public void ThrowAt(int request_index, Exception error) => Throws[request_index] = error;

        private sealed class RecordingHandler(RecordingFactory owner) : DelegatingHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            {
                owner.Requests.Add(request.RequestUri!);

                if (owner.Throws.TryGetValue(owner.Requests.Count - 1, out var error)) throw error;

                return base.SendAsync(request, ct);
            }
        }
    }
}
