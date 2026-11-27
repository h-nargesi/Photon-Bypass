using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Domain.Servers.Model;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.ServerBridge;
using PhotonBypass.ServerBridge.Tunnel;
using PhotonBypass.Test.Mock.MockServerBridge;

namespace PhotonBypass.Test.Facts.BasicFunctions.ServerBridge;

/// <summary>
/// The channel steps of the connection test (Docs/25 D8–D9) on a throw-away
/// session: the tcp step against loopback listeners, the host-key decision
/// per transport, and the forward step of the tunnel transport.
/// </summary>
public class ServerChannelProbeTest : IDisposable
{
    private readonly List<TcpListener> listeners = [];

    private readonly FakeSshConnector connector = new();

    private readonly ServerChannelProbe probe;

    private readonly int ssh_port;

    public ServerChannelProbeTest()
    {
        probe = new ServerChannelProbe(connector, Options.Create(new ServerBridgeOptions()), TimeProvider.System);

        // the tcp step connects for real: an SSH endpoint that listens on loopback
        ssh_port = Listen().Port;
    }

    public void Dispose()
    {
        foreach (var listener in listeners) listener.Stop();
    }

    [Fact]
    public async Task Direct_WithoutPin_GoesOnUnpinned_WithoutAForwardStep()
    {
        // today's direct rows: a password, no key and no pin
        var server = Server(transport: ServerTransport.Direct, pin: "", key: "");

        connector.OnConnect = _ => new FakeSshChannel
        {
            HostKey = new HostKeyCheck(TunnelFacts.PresentedKey, "ssh-rsa", HostKeyStatus.Unpinned),
        };

        await using var result = await probe.ProbeAsync(server);

        result.Steps.Select(step => step.Step).Should().Equal(ConnectionStepModel.Tcp, ConnectionStepModel.HostKey, ConnectionStepModel.Auth);
        result.Passed.Should().BeTrue();
        result.ForwardedLocalPort.Should().BeNull();
        result.HostKeySha256.Should().Be(TunnelFacts.PresentedKey);

        result.Steps[1].Detail.Should().Contain("بدون پین").And.Contain(TunnelFacts.PresentedKey);
        result.Steps[2].Detail.Should().Contain("پسورد");

        connector.Endpoints.Single().HostKeyPolicy.Should().Be(HostKeyPolicy.Optional);

        // the session outlives the channel steps: the caller disposes it after the service step
        connector.LastChannel!.Disconnected.Should().BeFalse();
    }

    [Fact]
    public async Task Tunnel_WithoutPin_StopsAfterTheHostKey_AndReportsTheFingerprint()
    {
        connector.OnConnect = _ => new FakeSshChannel
        {
            IsConnected = false,
            HostKey = new HostKeyCheck(TunnelFacts.PresentedKey, "ssh-ed25519", HostKeyStatus.Discovered),
        };

        await using var result = await probe.ProbeAsync(Server(pin: ""));

        connector.Endpoints.Single().HostKeyPolicy.Should().Be(HostKeyPolicy.Discover);

        result.Steps.Select(step => step.Step).Should().Equal(ConnectionStepModel.Tcp, ConnectionStepModel.HostKey, ConnectionStepModel.Auth);

        result.Steps[0].Passed.Should().BeTrue();
        result.Steps[1].Passed.Should().BeTrue();
        result.Steps[1].Detail.Should().Contain("کشف شد").And.Contain(TunnelFacts.PresentedKey);

        // the fingerprint is learned, but the test stops before authenticating anything
        var auth = result.Steps[2];

        auth.Passed.Should().BeFalse();
        auth.Detail.Should().Contain("کلید میزبان").And.Contain("ثبت");

        result.Passed.Should().BeFalse();
        result.HostKeySha256.Should().Be(TunnelFacts.PresentedKey);
        result.ForwardedLocalPort.Should().BeNull();
    }

    [Fact]
    public async Task Tunnel_WithPin_RunsTheForwardStep_AndKeepsTheSessionOpenForTheServiceStep()
    {
        var service = Listen();
        var forwarded = Listen();

        var channel = new FakeSshChannel { NextForwardPort = forwarded.Port };

        connector.OnConnect = _ => channel;

        await using var result = await probe.ProbeAsync(Server(web: Web(port: service.Port)));

        result.Steps.Select(step => step.Step).Should().Equal(
            ConnectionStepModel.Tcp, ConnectionStepModel.HostKey, ConnectionStepModel.Auth, ConnectionStepModel.Forward);

        result.Passed.Should().BeTrue();
        result.ForwardedLocalPort.Should().Be(forwarded.Port);
        result.Steps[2].Detail.Should().Contain("کلید خصوصی");
        result.Steps[3].Detail.Should().Contain("کانال باز شد").And.Contain($"پورت سرویس {service.Port}");

        connector.Endpoints.Single().HostKeyPolicy.Should().Be(HostKeyPolicy.Required);
        channel.Disconnected.Should().BeFalse();

        await result.DisposeAsync();

        channel.Disconnected.Should().BeTrue("the session lives exactly as long as the result");
    }

    [Theory]
    [InlineData(ServerTransport.Direct)]
    [InlineData(ServerTransport.SshTunnel)]
    public async Task MismatchedPin_StopsAtTheHostKey_OnBothTransports(ServerTransport transport)
    {
        connector.OnConnect = endpoint => throw new SshConnectException(
            SshConnectFailure.HostKeyMismatch, endpoint, TunnelFacts.PresentedKey);

        await using var result = await probe.ProbeAsync(Server(transport: transport, pin: TunnelFacts.PinnedKey));

        var last = result.Steps.Should().ContainSingle(step => !step.Passed).Subject;

        last.Step.Should().Be(ConnectionStepModel.HostKey);
        last.Detail.Should().Be(SshErrors.HostKeyMismatchMessage);

        result.Steps.Select(step => step.Step).Should().Equal(ConnectionStepModel.Tcp, ConnectionStepModel.HostKey);
        result.Passed.Should().BeFalse();
        result.HostKeySha256.Should().Be(TunnelFacts.PresentedKey, "the refused key is what the admin compares");
    }

    [Fact]
    public async Task AuthenticationFailure_IsTheAuthStep_WithTheStageAndDuration()
    {
        connector.OnConnect = endpoint => throw new SshConnectException(
            SshConnectFailure.Authentication, endpoint, TunnelFacts.PresentedKey, "authentication", 812);

        await using var result = await probe.ProbeAsync(Server());

        var last = result.Steps[^1];

        last.Step.Should().Be(ConnectionStepModel.Auth);
        last.Detail.Should().Contain(SshErrors.AuthenticationMessage).And.Contain("authentication").And.Contain("812 ms");

        result.Steps.Select(step => step.Step).Should().Equal(ConnectionStepModel.Tcp, ConnectionStepModel.Auth);
        result.Steps[0].Passed.Should().BeTrue();
    }

    [Fact]
    public async Task HandshakeTimeout_AfterTcp_IsTheAuthStep()
    {
        connector.OnConnect = endpoint => throw new SshConnectException(
            SshConnectFailure.Timeout, endpoint, null, "key-exchange", 15_000);

        await using var result = await probe.ProbeAsync(Server());

        var last = result.Steps[^1];

        last.Step.Should().Be(ConnectionStepModel.Auth);
        last.Detail.Should().Contain(SshErrors.ConnectMessage).And.Contain("key-exchange");
    }

    [Fact]
    public async Task TunnelRow_WithoutAKeyButWithAPin_FailsAtAuth_WithoutConnecting()
    {
        await using var result = await probe.ProbeAsync(Server(key: ""));

        var last = result.Steps[^1];

        last.Step.Should().Be(ConnectionStepModel.Auth);
        last.Detail.Should().Be("کلید خصوصی SSH معتبر نیست!");
        connector.Connects.Should().Be(0);
    }

    [Fact]
    public async Task Tunnel_ServerWithoutAManagementService_HasNoForwardStep()
    {
        // a NAS reached over SSH only: nothing to forward, so the channel ends after authentication
        connector.OnConnect = _ => new FakeSshChannel();

        await using var result = await probe.ProbeAsync(Server(features: ServerFeature.Nas | ServerFeature.Ovpn, web: null));

        result.Steps.Select(step => step.Step).Should().Equal(
            ConnectionStepModel.Tcp, ConnectionStepModel.HostKey, ConnectionStepModel.Auth);
        result.Passed.Should().BeTrue();
        result.ForwardedLocalPort.Should().BeNull();
        connector.LastChannel!.StartForwardCalls.Should().Be(0);
    }

    [Fact]
    public async Task Tunnel_ForwardStep_FailsWithoutAServicePort()
    {
        connector.OnConnect = _ => new FakeSshChannel();

        await using var result = await probe.ProbeAsync(Server(web: new WebApiConfig { HostName = "10.0.0.7" }));

        var last = result.Steps[^1];

        last.Step.Should().Be(ConnectionStepModel.Forward);
        last.Detail.Should().Be("در حالت تانل، آدرس سرویس باید `127.0.0.1` با پورت مشخص باشد!");
        result.ForwardedLocalPort.Should().BeNull();
        connector.LastChannel!.StartForwardCalls.Should().Be(0);
    }

    [Fact]
    public async Task Tunnel_ForwardStep_FailsWhenTheLoopbackConnectionIsRefused()
    {
        var channel = new FakeSshChannel { NextForwardPort = ClosedPort() };

        connector.OnConnect = _ => channel;

        await using var result = await probe.ProbeAsync(Server());

        var last = result.Steps[^1];

        last.Step.Should().Be(ConnectionStepModel.Forward);
        last.Passed.Should().BeFalse();
        last.Detail.Should().Contain(SshErrors.ConnectMessage).And.Contain("SocketException");
    }

    [Fact]
    public async Task Tunnel_ForwardStep_FailsWhenTheSessionCannotForward()
    {
        connector.OnConnect = _ => new FakeSshChannel { IsConnected = false };

        await using var result = await probe.ProbeAsync(Server());

        var last = result.Steps[^1];

        last.Step.Should().Be(ConnectionStepModel.Forward);
        last.Passed.Should().BeFalse();
    }

    [Fact]
    public async Task TcpRefused_StopsAtTcp_WithoutConnecting()
    {
        await using var result = await probe.ProbeAsync(Server(ssh_port: ClosedPort()));

        var step = result.Steps.Should().ContainSingle().Subject;

        step.Step.Should().Be(ConnectionStepModel.Tcp);
        step.Detail.Should().Contain("اتصال TCP برقرار نشد").And.Contain("ConnectionRefused");
        result.Passed.Should().BeFalse();
        connector.Connects.Should().Be(0);
    }

    [Fact]
    public async Task MissingSshConfiguration_FailsAtTcp()
    {
        var server = Server();

        server.JsonConfig = JsonSerializer.Serialize(new ServerConfiguration { Transport = ServerTransport.SshTunnel });

        await using var result = await probe.ProbeAsync(server);

        var step = result.Steps.Should().ContainSingle().Subject;

        step.Detail.Should().Be("پیکربندی SSH این سرور تنظیم نشده است!");
    }

    [Fact]
    public async Task OutOfRangeSshPort_FailsAtTcp()
    {
        await using var result = await probe.ProbeAsync(Server(ssh_port: 70000));

        result.Steps.Single().Detail.Should().Be("پورت SSH نامعتبر است!");
        connector.Connects.Should().Be(0);
    }

    [Fact]
    public async Task Steps_CarryPortsFingerprintsAndGeneralTextOnly()
    {
        var service = Listen();
        var forwarded = Listen();

        connector.OnConnect = _ => new FakeSshChannel { NextForwardPort = forwarded.Port };

        await using var result = await probe.ProbeAsync(Server(web: Web(port: service.Port)));

        var json = JsonSerializer.Serialize(result.Steps);

        json.Should().Contain(TunnelFacts.PinnedKey);
        json.Should().NotContainAny(TunnelFacts.KeyText, TunnelFacts.PassPhrase, TunnelFacts.Password, "api-secret", "panel");
    }

    private ServerEntity Server(
        ServerTransport transport = ServerTransport.SshTunnel, string pin = TunnelFacts.PinnedKey,
        string key = TunnelFacts.KeyText, int? ssh_port = null, WebApiConfig? web = null,
        ServerFeature features = ServerFeature.Xui | ServerFeature.V2ray)
    {
        return TunnelFacts.Server(
            ip: "127.0.0.1", transport: transport, pin: pin, key: key, features: features,
            ssh_port: ssh_port ?? this.ssh_port, web: web);
    }

    private static WebApiConfig Web(int port) => new() { HostName = "127.0.0.1", Port = port, Username = "admin", Password = "api-secret" };

    private (TcpListener Listener, int Port) Listen()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);

        listener.Start();
        listeners.Add(listener);

        return (listener, ((IPEndPoint)listener.LocalEndpoint).Port);
    }

    private int ClosedPort()
    {
        var (listener, port) = Listen();

        listener.Stop();

        return port;
    }
}
