using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Domain.Servers.Model;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.ErrorHandler;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.ServerBridge.Tunnel;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;
using PhotonBypass.Test.Mock.MockServerBridge;
using PhotonBypass.Xui.WebService;

namespace PhotonBypass.Test.Facts.Xui;

/// <summary>
/// The <c>SshTunnel</c> transport against the Xui-Test VM (Docs/25 §5.1, §6):
/// the forward-only key of the <c>xui-panel</c> user, the pinned host key, the
/// loopback port-forward to the panel, and what the pool and the adapter do when
/// that session is lost. The VLESS data plane is not part of it — it keeps going
/// straight to the VM; only the panel API rides the tunnel.
/// </summary>
[Collection(XuiPanelTestCollection.Name)]
public class XuiPanelTunnelTest : OutSourceLevelServiceInitializer
{
    private const string VmKey = XuiPanelInitializer.VmKey;

    private const string AuthorizedKeys = "/home/xui-panel/.ssh/authorized_keys";

    private static readonly ServerFeature Features = ServerFeature.Xui | ServerFeature.V2ray;

    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);

    private static ServerEntity Server(WebApiConfig? web = null, string? pin = null, string? keyText = null) =>
        TestVmTunnel.Server(VmKey, TestVmTunnel.XuiServerId, Features, web, pin, keyText);

    private static ISshTunnelPool Pool(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<ISshTunnelPool>();

    private static Lazy<IXuiApi> Api(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<Lazy<IXuiApi>>();

    [Fact]
    public async Task FullClientLifecycle_RidesTheTunnel()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(VmKey, this);

        var server = Server();
        var api = Api(scope);
        await XuiPanelCarrier.AttachAsync(api, server);

        var email = XuiPanelTestServer.Email("tl-lc");
        var uuid = Guid.NewGuid().ToString();
        var rotated = Guid.NewGuid().ToString();

        try
        {
            await api.Value.AddClient(server, XuiPanelClientLifecycleTest.Client(email, uuid,
                total_gb: 26_843_545_600L, expiry: 1_900_000_000_000L, limit_ip: 2));

            var client = (await api.Value.ListClients(server)).Should().ContainSingle(c => c.Email == email).Subject;

            client.Uuid.Should().Be(uuid);
            client.TotalTrafficBytes.Should().Be(26_843_545_600L);
            client.ExpiryTime.Should().Be(1_900_000_000_000L);
            client.LimitIp.Should().Be(2);

            await api.Value.UpdateClient(server, uuid, XuiPanelClientLifecycleTest.Client(email, rotated));

            var updated = (await api.Value.ListClients(server)).Should().ContainSingle(c => c.Email == email).Subject;

            updated.Uuid.Should().Be(rotated);

            await api.Value.ResetClientTraffic(server, email);

            (await api.Value.ListClientTraffic(server)).Should().ContainSingle(stats => stats.Email == email);

            await api.Value.RemoveClient(server, rotated);

            (await api.Value.ListClients(server)).Should().NotContain(c => c.Email == email);
            (await api.Value.ListClientTraffic(server)).Should().NotContain(stats => stats.Email == email);
        }
        finally
        {
            await RemoveIfPresentAsync(api, server, uuid, rotated);
        }
    }

    [Fact]
    public async Task Onlines_ThroughTheTunnel_SeparateOnlineFromOffline()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(VmKey, this);

        var server = Server();
        var api = Api(scope);
        await XuiPanelCarrier.AttachAsync(api, server);

        var online = XuiPanelTestServer.Email("tl-online");
        var idle = XuiPanelTestServer.Email("tl-idle");
        var online_uuid = Guid.NewGuid().ToString();
        var idle_uuid = Guid.NewGuid().ToString();

        await api.Value.AddClient(server, new XuiClientModel { Email = online, Uuid = online_uuid });
        await api.Value.AddClient(server, new XuiClientModel { Email = idle, Uuid = idle_uuid });

        VlessWsClient? connection = null;

        try
        {
            var inbound = await api.Value.GetInbound(server);
            var path = JsonDocument.Parse(inbound.StreamSettings!).RootElement
                .GetProperty("wsSettings").GetProperty("path").GetString()!;
            var host = XuiPanelTestServer.Vm.Ip;

            // the data connection goes straight to the VM: sshd waits on the pre-auth
            // version line, so it stays open for the whole poll
            connection = await VlessWsClient.ConnectAsync(host, inbound.Port, path, online_uuid, host, 22);

            await XuiPanelTrafficProbe.PollUntilAsync(async () =>
            {
                await connection.NudgeAsync();

                var listed = await api.Value.ListOnlineClients(server);

                return listed.Any(entry => entry.Email == online) && !listed.Any(entry => entry.Email == idle);
            }, $"'{online}' to be listed and '{idle}' not to be", cap: TimeSpan.FromSeconds(300));
        }
        finally
        {
            if (connection is not null)
            {
                await connection.DisposeAsync();
            }

            await RemoveIfPresentAsync(api, server, online_uuid, idle_uuid);
        }
    }

    [Fact]
    public async Task ConcurrentRequests_ShareOneSessionAndOneGeneration()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(VmKey, this);

        var server = Server();
        var api = Api(scope);
        await XuiPanelCarrier.AttachAsync(api, server);

        await Task.WhenAll(
            api.Value.ListClients(server),
            api.Value.ListOnlineClients(server),
            api.Value.GetInbound(server),
            api.Value.ListClientTraffic(server));

        (await Pool(scope).GetAsync(server)).Generation.Should().Be(1);
    }

    [Fact]
    public async Task WrongPin_IsRefusedBeforeAuthentication()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(VmKey, this);

        var server = Server();
        var wrong = Server(pin: TunnelFacts.PinnedKey);
        var pool = Pool(scope);

        var refused = (await Assert.ThrowsAsync<UserException>(() => pool.GetAsync(wrong).AsTask()))!;

        refused.UserMessage.Should().Be(SshErrors.HostKeyMismatchMessage);

        // through the adapter the HTTP stack wraps the refusal; the wording survives in the chain
        var wrapped = await Assert.ThrowsAsync<HttpRequestException>(() => Api(scope).Value.ListClients(wrong));

        Chain(wrapped).OfType<UserException>().Should().Contain(error => error.UserMessage == SshErrors.HostKeyMismatchMessage);

        // a refused connect is not a generation: the right pin still starts at one
        (await pool.GetAsync(server)).Generation.Should().Be(1);
    }

    [Fact]
    public async Task RightPin_UnauthorizedKey_IsRefusedByAuthentication()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(VmKey, this);

        // a valid key the VM never saw: the tunnel key of the router
        var foreign = Server(keyText: TestVmTunnel.TunnelKey("Mikrotik-Base"));

        var refused = (await Assert.ThrowsAsync<UserException>(() => Pool(scope).GetAsync(foreign).AsTask()))!;

        refused.UserMessage.Should().Be(SshErrors.AuthenticationMessage);
    }

    [Fact]
    public async Task ExecWithTheForwardOnlyKey_FailsWithExitStatusOnly()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(VmKey, this);

        using var node = await scope.ServiceProvider.GetRequiredService<ISshHandler>().ConnectTo(Server());

        // the forced command of the authorized_keys line answers nothing and exits non-zero
        node.Execute("id", out var result).Should().BeFalse();

        result.Should().Be("exit-status 1");
    }

    [Fact]
    public async Task ForwardToAForbiddenPort_IsClosedWithoutARequest_AndTheSessionStaysUsable()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(VmKey, this);

        var server = Server();
        var api = Api(scope);
        var session = await Pool(scope).GetAsync(server);

        // permitopen names the panel port only
        var forbidden = session.ForwardedPort(22);

        using (var tcp = new TcpClient())
        {
            await tcp.ConnectAsync(IPAddress.Loopback, forbidden);

            using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            // EOF before a single byte: the server refused the channel
            (await tcp.GetStream().ReadAsync(new byte[1], limit.Token)).Should().Be(0);
        }

        (await api.Value.GetInbound(server)).Id.Should().Be(XuiPanelTestServer.Vm.InboundId);
        (await Pool(scope).GetAsync(server)).Generation.Should().Be(session.Generation);
    }

    [Fact]
    public async Task SessionDroppedMidway_ReadsContinueOnANewGeneration_AndAWriteNeverDuplicates()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(VmKey, this);

        var server = Server();
        var api = Api(scope);
        var pool = Pool(scope);
        await XuiPanelCarrier.AttachAsync(api, server);

        var before = (await pool.GetAsync(server)).Generation;

        await DropTunnelSessionAsync();

        (await api.Value.ListClients(server)).Should().NotBeEmpty("the carrier client is still there");

        var after_read = (await pool.GetAsync(server)).Generation;

        after_read.Should().Be(before + 1);

        // a write is never sent twice (Docs/25 D7): it either reaches the panel once or not at all
        await DropTunnelSessionAsync();

        var email = XuiPanelTestServer.Email("tl-dup");
        var uuid = Guid.NewGuid().ToString();

        try
        {
            try
            {
                await api.Value.AddClient(server, new XuiClientModel { Email = email, Uuid = uuid });
            }
            catch (Exception error) when (error is HttpRequestException || XuiTransportDeath.Is(error))
            {
                // the write died on the wire and was not repeated
            }

            (await api.Value.ListClients(server)).Count(client => client.Email == email).Should().BeLessThanOrEqualTo(1);

            // one reconnect per drop, never more
            (await pool.GetAsync(server)).Generation.Should().Be(before + 2);
        }
        finally
        {
            await RemoveIfPresentAsync(api, server, uuid);
        }
    }

    [Fact]
    public async Task SessionDroppedMidway_AHeldCommandConnectionReportsTheLoss()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(VmKey, this);

        var server = Server();
        var session = await Pool(scope).GetAsync(server);
        var connection = await session.OpenCommandChannelAsync(CommandTimeout);

        try
        {
            await DropTunnelSessionAsync();

            var error = (await Assert.ThrowsAsync<UserException>(() => Task.Run(() => connection.Execute("id", out _))))!;

            error.UserMessage.Should().Be(SshErrors.ConnectMessage);
        }
        finally
        {
            connection.Dispose();
        }

        // the next command connection runs on a session of the next generation
        using (var next = await scope.ServiceProvider.GetRequiredService<ISshHandler>().ConnectTo(server))
        {
            next.Execute("id", out var result).Should().BeFalse();
            result.Should().Be("exit-status 1");
        }

        (await Pool(scope).GetAsync(server)).Generation.Should().Be(session.Generation + 1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/")]
    [InlineData(" / ")]
    public async Task BasePathForms_AllBuildTheSameUrl(string? base_path)
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(VmKey, this);

        var web = TestVmTunnel.ServiceAddress(VmKey, Features);
        web.BasePath = base_path;

        var inbound = await Api(scope).Value.GetInbound(Server(web));

        inbound.Id.Should().Be(XuiPanelTestServer.Vm.InboundId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuthorizedKeys_HealthyAndOddShape_Authenticate(bool odd_shape)
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(VmKey, this);

        var server = Server();
        var pool = Pool(scope);
        var original = await TestVmAdmin.Run(VmKey, $"sudo cat {AuthorizedKeys}");

        original.ExitStatus.Should().Be(0, original.Error);
        original.Output.Should().Contain("restrict,port-forwarding");

        // the fixture file as the script wrote it
        (await pool.GetAsync(server)).Generation.Should().Be(1);

        try
        {
            if (odd_shape)
            {
                // a blank first line and no trailing newline (Docs/25 appendix B, finding 2)
                await TestVmAdmin.Run(VmKey, WriteAuthorizedKeys($"\n{original.Output.TrimEnd('\n')}"));
            }

            // the pooled session stays valid for its whole lifetime: a fresh login needs a new one
            var live = await pool.GetAsync(server);

            pool.Fault(server.Id, live.Generation, "test: authenticate again");

            (await pool.GetAsync(server)).Generation.Should().Be(live.Generation + 1);

            (await Api(scope).Value.GetInbound(server)).Id.Should().Be(XuiPanelTestServer.Vm.InboundId);
        }
        finally
        {
            // written back byte for byte and read back below: the VM is left as the fixture made it
            await TestVmAdmin.Run(VmKey, WriteAuthorizedKeys(original.Output));
        }

        (await TestVmAdmin.Run(VmKey, $"sudo cat {AuthorizedKeys}")).Output.Should().Be(original.Output);
    }

    [Fact]
    public async Task ConnectionProbe_Discover_ReturnsTheRecordedPin_AndStopsBeforeAuth()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(VmKey, this);

        await using (var channel = await scope.ServiceProvider.GetRequiredService<IServerChannelProbe>()
                           .ProbeAsync(Server(pin: "")))
        {
            channel.Steps.Select(step => (step.Step, step.Passed))
                .Should().Equal((ConnectionStepModel.Tcp, true), (ConnectionStepModel.HostKey, true), (ConnectionStepModel.Auth, false));

            channel.Steps[1].Detail.Should().Contain("کشف شد");
            channel.Steps[2].Detail.Should().Contain("کلید میزبان این سرور ثبت نشده است");

            // the fingerprint to register is the one the VM script recorded
            channel.HostKeySha256.Should().Be(TestVmTunnel.Pin(VmKey));

            channel.ForwardedLocalPort.Should().BeNull();
            channel.Passed.Should().BeFalse();
        }
    }

    [Fact]
    public async Task ConnectionProbe_WithPin_PassesEveryStepThroughTheServiceLogin()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<XuiPanelInitializer>(VmKey, this);

        await using (var channel = await scope.ServiceProvider.GetRequiredService<IServerChannelProbe>().ProbeAsync(Server()))
        {
            channel.Steps.Select(step => (step.Step, step.Passed)).Should().Equal(
                (ConnectionStepModel.Tcp, true),
                (ConnectionStepModel.HostKey, true),
                (ConnectionStepModel.Auth, true),
                (ConnectionStepModel.Forward, true));

            channel.HostKeySha256.Should().Be(TestVmTunnel.Pin(VmKey));
            channel.ForwardedLocalPort.Should().NotBeNull();

            // the service step, as ServerConnectionTester appends it (which itself needs an admin actor)
            var service = await scope.ServiceProvider
                .GetRequiredKeyedService<IServiceProbe>(ServerFeature.Xui)
                .ProbeAsync(Server(), channel.ForwardedLocalPort);

            service.Step.Should().Be(ConnectionStepModel.Service);
            service.Passed.Should().BeTrue(service.Detail);
        }
    }

    /// <summary>
    /// Forcibly closes the pooled SSH session (Docs/25 §6): the session processes of the
    /// tunnel user on the VM. sshd itself stays up for the next connection — restarting it
    /// would not do, its unit keeps established sessions (<c>KillMode=process</c>).
    /// </summary>
    private static Task<SshCommandResult> DropTunnelSessionAsync() =>
        TestVmAdmin.Run(VmKey, $"sudo pkill -KILL -u {TestVmOptions.Get(VmKey).TunnelUser}");

    private static string WriteAuthorizedKeys(string content) =>
        $"echo '{Convert.ToBase64String(Encoding.UTF8.GetBytes(content))}' | base64 -d | sudo tee {AuthorizedKeys} >/dev/null";

    private static async Task RemoveIfPresentAsync(Lazy<IXuiApi> api, ServerEntity server, params string[] uuids)
    {
        foreach (var uuid in uuids)
        {
            try
            {
                await api.Value.RemoveClient(server, uuid);
            }
            catch (Exception)
            {
                // already gone, or its key was rotated: the snapshot restore is the final sanitizer
            }
        }
    }

    private static IEnumerable<Exception> Chain(Exception error)
    {
        for (var current = error; current != null; current = current.InnerException)
        {
            yield return current;
        }
    }
}
