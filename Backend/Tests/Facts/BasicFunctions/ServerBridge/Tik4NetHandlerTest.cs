using System.Net.Sockets;
using FluentAssertions;
using Moq;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Domain.Servers.Types;
using Microsoft.Extensions.Options;
using PhotonBypass.ErrorHandler;
using PhotonBypass.ServerBridge;
using PhotonBypass.ServerBridge.Tik4net;
using PhotonBypass.ServerBridge.Tunnel;
using PhotonBypass.Test.Mock.MockServerBridge;
using tik4net;

namespace PhotonBypass.Test.Facts.BasicFunctions.ServerBridge;

/// <summary>
/// The User Manager API connection over both transports (Docs/25 D3): the
/// direct transport addresses the configured host as before; the tunnel
/// transport addresses the loopback port of the pooled forward and reports
/// an open failure back to the pool.
/// </summary>
public class Tik4NetHandlerTest
{
    private const int Forwarded = 41_001;

    private readonly Mock<ITikConnection> connection = new();

    private readonly FakeTunnelPool pool = new();

    private readonly List<TikConnectionType> created = [];

    public Tik4NetHandlerTest()
    {
        connection.Setup(c => c.OpenAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        pool.LocalPorts[8728] = Forwarded;
        pool.LocalPorts[8729] = Forwarded + 1;
    }

    [Fact]
    public async Task Direct_OpensTheConfiguredHostAndPort_WithTheApiCredentials()
    {
        using (await Handler().ConnectTo(Server(ServerTransport.Direct, port: 8728)))
        {
            connection.Verify(c => c.Dispose(), Times.Never, "an opened connection belongs to the caller");
        }

        Opened().Should().ContainSingle().Which.Should().Be(("10.0.0.7", 8728, "admin", "api-secret"));
        pool.Asked.Should().BeEmpty("the direct transport never touches the pool");
    }

    [Theory]
    [InlineData(false, 8728, TikConnectionType.Api)]
    [InlineData(true, 8729, TikConnectionType.ApiSsl)]
    public async Task Direct_WithoutAPort_UsesTheApiDefaultPort(bool ssl, int expected_port, TikConnectionType expected_type)
    {
        using (await Handler().ConnectTo(Server(ServerTransport.Direct, port: null, ssl: ssl)))
        {
        }

        Opened().Single().port.Should().Be(expected_port);
        created.Should().Equal(expected_type);
    }

    [Theory]
    [InlineData(false, 8728, TikConnectionType.Api)]
    [InlineData(true, 8729, TikConnectionType.ApiSsl)]
    public async Task Tunnel_OpensTheForwardedLoopbackPort(bool ssl, int service_port, TikConnectionType expected_type)
    {
        using (await Handler().ConnectTo(Server(ServerTransport.SshTunnel, port: null, ssl: ssl)))
        {
        }

        Opened().Single().Should().Be(("127.0.0.1", pool.LocalPorts[service_port], "admin", "api-secret"));
        created.Should().Equal(expected_type);
        pool.Asked.Should().ContainSingle();
    }

    [Fact]
    public async Task Tunnel_WithAConfiguredPort_ForwardsThatPort()
    {
        using (await Handler().ConnectTo(Server(ServerTransport.SshTunnel, port: 8729)))
        {
        }

        Opened().Single().port.Should().Be(Forwarded + 1);
    }

    [Fact]
    public async Task Tunnel_OpenFailure_FaultsThePool_Disposes_AndLeavesAConnectUserException()
    {
        connection.Setup(c => c.OpenAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new IOException("connection closed by the remote host"));

        var error = (await Assert.ThrowsAsync<UserException>(() => Handler().ConnectTo(Server(ServerTransport.SshTunnel))))!;

        error.UserMessage.Should().Be(SshErrors.ConnectMessage);
        error.Message.Should().Contain("8728").And.NotContain("api-secret");

        pool.Faults.Should().ContainSingle().Which.Should().Be((7, 1, "User Manager API open failed (IOException)"));
        connection.Verify(c => c.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Tunnel_OpenTimeout_FaultsThePool_Disposes_AndLeavesAConnectUserException()
    {
        connection.Setup(c => c.OpenAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new TaskCompletionSource().Task);

        var error = (await Assert.ThrowsAsync<UserException>(() => Handler(timeout_seconds: 1).ConnectTo(Server(ServerTransport.SshTunnel))))!;

        error.UserMessage.Should().Be(SshErrors.ConnectMessage);
        error.Message.Should().Contain("8728");

        pool.Faults.Should().ContainSingle().Which.Should().Be((7, 1, "User Manager API open timeout"));
        connection.Verify(c => c.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Tunnel_LoginFailure_KeepsTheSession_AndRethrowsThePanelsError()
    {
        connection.Setup(c => c.OpenAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new TikConnectionLoginException(new Exception("invalid user name or password")));

        var error = await Assert.ThrowsAsync<TikConnectionLoginException>(() => Handler().ConnectTo(Server(ServerTransport.SshTunnel)));

        error.Message.Should().Contain("invalid user name or password");

        pool.Faults.Should().BeEmpty("the service answered through the channel: the tunnel session is healthy");
        connection.Verify(c => c.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Direct_OpenTimeout_LeavesAConnectUserException_AndDisposes()
    {
        connection.Setup(c => c.OpenAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new TaskCompletionSource().Task);

        var error = (await Assert.ThrowsAsync<UserException>(
            () => Handler(timeout_seconds: 1).ConnectTo(Server(ServerTransport.Direct))))!;

        error.UserMessage.Should().Be(SshErrors.ConnectMessage);
        error.Message.Should().Contain("10.0.0.7");
        pool.Faults.Should().BeEmpty();
        connection.Verify(c => c.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Direct_OpenFailure_RethrowsAsBefore()
    {
        connection.Setup(c => c.OpenAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new SocketException());

        await Assert.ThrowsAsync<SocketException>(() => Handler().ConnectTo(Server(ServerTransport.Direct)));

        pool.Faults.Should().BeEmpty();
        connection.Verify(c => c.Dispose(), Times.Once);
    }

    [Fact]
    public async Task WithoutAWebConfiguration_ThrowsBeforeThePoolIsTouched()
    {
        var server = new ServerEntity
        {
            Id = 7,
            Name = "T7",
            IpAddress = "10.0.0.7",
            JsonConfig = """{"Transport":"SshTunnel","SshConfig":{"Username":"panel","Port":22}}""",
        };

        var error = await Assert.ThrowsAsync<Exception>(() => Handler().ConnectTo(server));

        error.Message.Should().Contain("web-api configuration is not set");
        pool.Asked.Should().BeEmpty();
    }

    private Tik4NetHandler Handler(int timeout_seconds = 30)
    {
        return new Tik4NetHandler(
            Options.Create(new ServerBridgeOptions { Tik4NetOpenTimeoutSeconds = timeout_seconds }),
            new Lazy<ISshTunnelPool>(() => pool),
            type =>
            {
                created.Add(type);

                return connection.Object;
            });
    }

    private static ServerEntity Server(ServerTransport transport, int? port = 8728, bool ssl = false)
    {
        return TunnelFacts.Server(
            transport: transport,
            web: new WebApiConfig
            {
                HostName = transport == ServerTransport.SshTunnel ? "127.0.0.1" : "10.0.0.7",
                Port = port,
                Ssl = ssl,
                Username = "admin",
                Password = "api-secret",
            });
    }

    private List<(string host, int port, string user, string password)> Opened()
    {
        return [.. connection.Invocations
            .Where(invocation => invocation.Method.Name == nameof(ITikConnection.OpenAsync) && invocation.Arguments.Count == 4)
            .Select(invocation => ((string)invocation.Arguments[0], (int)invocation.Arguments[1],
                (string)invocation.Arguments[2], (string)invocation.Arguments[3]))];
    }
}
