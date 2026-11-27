using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.Options;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Model;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Infra.Nas;
using PhotonBypass.Infra.Radius;
using PhotonBypass.Mikrotik.Radius.Application;
using PhotonBypass.Mikrotik.Radius.Model;
using PhotonBypass.ServerBridge;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.ServerBridge.Ssh;
using PhotonBypass.ServerBridge.Tik4net;
using PhotonBypass.ServerBridge.Tunnel;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;
using PhotonBypass.Test.Mock.MockServerBridge;
using tik4net;
using tik4net.Objects;
using Xunit.Abstractions;
using OperatingSystem = PhotonBypass.Domain.Servers.Types.OperatingSystem;

namespace PhotonBypass.Test.Facts.Mikrotik;

/// <summary>
/// The <c>SshTunnel</c> transport against the local Mikrotik-Base VM (Docs/25
/// §5.2, §6): the key-authenticated session of the five-policy <c>panel</c> user,
/// the loopback port-forward to the User Manager API, and the channel RouterOS
/// reports command failures on. RouterOS 7.20.6 answers a failed command with
/// text on stdout, usually with exit status 0: what these tests see of that
/// channel goes to the test output (Docs/25 §4.3); they assert only what the
/// panel relies on — that such a command never passes as a success.
/// </summary>
[Collection(MikrotikBaseTestCollection.Name)]
public class MikrotikTunnelTest(ITestOutputHelper output) : OutSourceLevelServiceInitializer
{
    private const string VmKey = "Mikrotik-Base";

    private const string PanelGroup = "photon-panel";

    private const string OvpnCa = "LMTCA";

    private const string IdentityCommand = "/system identity print";

    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(30);

    private static readonly string[] RouterOsFailures =
    [
        "/ppp active remove *FFF",
        "/tool ping 127.0.0.1 count=1",
        $"/user group set {PanelGroup} comment=tunnel-test",
        "/nosuchcommand",
    ];

    private static int ApiPort => TestVmOptions.Get(VmKey).Port!.Value;

    private static ServerEntity Router(string? pin = null, string? keyText = null) =>
        TestVmTunnel.Server(VmKey, TestVmTunnel.MikrotikServerId, ServerFeature.Radius, pin: pin, keyText: keyText);

    private static ISshTunnelPool Pool(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<ISshTunnelPool>();

    [Fact]
    public async Task KeyAndPin_ForwardTheApi_AndTik4NetOpensOnTheForwardedPort()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<MikrotikInitializer>(VmKey, this);

        var router = Router();
        var pool = Pool(scope);
        var session = await pool.GetAsync(router);

        session.Generation.Should().Be(1);

        var forwarded = session.ForwardedPort(ApiPort);

        forwarded.Should().NotBe(ApiPort);

        // the listener is the panel's own loopback forward, never a public one
        IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners()
            .Should().Contain(listener => listener.Port == forwarded && listener.Address.Equals(IPAddress.Loopback))
            .And.NotContain(listener => listener.Port == forwarded && !listener.Address.Equals(IPAddress.Loopback));

        using (var connection = await scope.ServiceProvider.GetRequiredService<ITik4NetHandler>().ConnectTo(router))
        {
            connection.CreateCommand("/system/identity/print").ExecuteScalar("name").Should().Be("MikroTik");
        }

        // the API connection rode the pooled session: nothing was rebuilt for it
        (await pool.GetAsync(router)).Generation.Should().Be(session.Generation);
    }

    [Fact]
    public async Task WrongPin_IsRefusedBeforeAuthentication()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<MikrotikInitializer>(VmKey, this);

        var router = Router();
        var pool = Pool(scope);

        var error = (await Assert.ThrowsAsync<UserException>(
            () => pool.GetAsync(Router(pin: TunnelFacts.PinnedKey)).AsTask()))!;

        error.UserMessage.Should().Be(SshErrors.HostKeyMismatchMessage);

        // a refused connect is not a generation: the right pin still starts at one
        (await pool.GetAsync(router)).Generation.Should().Be(1);
    }

    [Fact]
    public async Task RightPin_UnauthorizedKey_IsRefusedByAuthentication()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<MikrotikInitializer>(VmKey, this);

        // a valid key the router never saw: the tunnel key of the x-ui VM
        var foreign = Router(keyText: TestVmTunnel.TunnelKey(XuiPanelInitializer.VmKey));

        var error = (await Assert.ThrowsAsync<UserException>(() => Pool(scope).GetAsync(foreign).AsTask()))!;

        error.UserMessage.Should().Be(SshErrors.AuthenticationMessage);
    }

    [Fact]
    public async Task CertificateOperations_WithTheFivePolicyGroup_IssueASignedClientCertificate()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<MikrotikInitializer>(VmKey, this);

        var router = Router();
        var username = NewUsername();
        var certificate = $"CLIENT_{username}";
        var template = Encoding.UTF8.GetString(await File.ReadAllBytesAsync("Data/config.ovpn"));
        var context = new CertContext { Realm = "Tunnel", CertFile = await File.ReadAllBytesAsync("Data/config.ovpn") };

        try
        {
            await scope.ServiceProvider.GetRequiredService<IMikrotikDirectService>().GetOVpnCertificate(router, username, context);

            var ovpn = Encoding.UTF8.GetString(context.CertFile);

            Section(ovpn, "cert").Should().Contain("-----BEGIN CERTIFICATE-----").And.NotBe(Section(template, "cert"));
            Section(ovpn, "key").Should().MatchRegex(@"^-----BEGIN ENCRYPTED PRIVATE KEY-----").And.NotBe(Section(template, "key"));

            // the delivered certificate is the one issued for this account
            using (var issued = new X509Certificate2(Convert.FromBase64String(Der(Section(ovpn, "cert")))))
            {
                issued.GetNameInfo(X509NameType.SimpleName, false).Should().Be(certificate);
            }

            (await TestVmAdmin.Run(VmKey, $"/certificate print detail where name=\"{certificate}\""))
                .Output.Should().Contain($"ca={OvpnCa}");
        }
        finally
        {
            await TestVmAdmin.Run(VmKey, $"/certificate remove [find name=\"{certificate}\"]");
            await TestVmAdmin.Run(VmKey, $"/file remove [find name=\"{certificate}.crt\"]");
            await TestVmAdmin.Run(VmKey, $"/file remove [find name=\"{certificate}.key\"]");
        }

        (await TestVmAdmin.Run(VmKey, $"/certificate print where name=\"{certificate}\"")).Output.Should().BeNullOrWhiteSpace();
        (await TestVmAdmin.Run(VmKey, $"/file print where name=\"{certificate}.crt\"")).Output.Should().BeNullOrWhiteSpace();
        (await TestVmAdmin.Run(VmKey, $"/file print where name=\"{certificate}.key\"")).Output.Should().BeNullOrWhiteSpace();
    }

    [Fact]
    public async Task PppPrintCommands_RunCleanly_AndARouterOsErrorSurfacesAsFailure()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<MikrotikInitializer>(VmKey, this);

        var router = Router();
        var nobody = NewUsername();

        using (var node = await scope.ServiceProvider.GetRequiredService<ISshHandler>().ConnectTo(router))
        {
            node.Execute($"/ppp active print where name=\"{nobody}\" uptime session-id caller-id limit-bytes-in", out var result)
                .Should().BeTrue();
            result.Should().BeNullOrWhiteSpace();

            // the panel's own remove shape: nothing matched, so nothing was removed and nothing is reported
            node.Execute("/ppp active remove [find session-id=0xABCDEF]", out result).Should().BeTrue();
            result.Should().BeNullOrWhiteSpace();

            // a failed command says so as text on stdout: the verdict must not take it for a success
            node.Execute("/ppp active remove *FFF", out result).Should().BeFalse();
            result.Should().Contain("no such item");
        }

        (await scope.ServiceProvider.GetRequiredService<IMikrotikDirectService>().GetActivePppConnections(router, nobody))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task UserManagerOperations_WithTheFivePolicyGroup()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<MikrotikInitializer>(VmKey, this);

        var router = Router();
        var username = NewUsername();
        var account = new VpnAccountEntity { RealmId = 1, Username = username, VpnPassword = username };
        var renewal = new RenewalEntity
        {
            SimultaneousUser = 1,
            TrafficLimit = 25 * StaticValues.BytesInGigLong,
            TimeLimitInDays = 10,
        };

        var accounts = scope.ServiceProvider.GetRequiredKeyedService<IInfraAccountRadiusSyncService>(RadiusType.UserManager);
        var sessions = scope.ServiceProvider.GetRequiredKeyedService<IInfraSessionRadiusSyncService>(RadiusType.UserManager);
        var handler = scope.ServiceProvider.GetRequiredService<ITik4NetHandler>();

        try
        {
            await accounts.SyncUserAndActive(router, account, renewal);

            (await UserAsync(handler, router, username)).Should().NotBeNull();

            (await accounts.ChangeVpnPassword(router, username, "it.tunnel.new-password")).Should().Be("it.tunnel.new-password");
            (await UserAsync(handler, router, username))!.Password.Should().Be("it.tunnel.new-password");

            await accounts.DeactivateUser(router, [username]);
            (await UserAsync(handler, router, username))!.Disabled.Should().BeTrue();

            await accounts.ActivateUser(router, [username]);
            (await UserAsync(handler, router, username))!.Disabled.Should().BeFalse();

            (await sessions.GetActiveConnections(router, username)).Should().BeEmpty();

            // no session to close: nothing is reported
            await sessions.CloseConnectionByUsername(router, username);

            using (var connection = await handler.ConnectTo(router))
            {
                // the panel's command (its full menu path) for an id no session has: a trap
                // from the library, never a silent success and never "no such command"
                // (the menu exists). RouterOS 7.20.6 does not phrase this trap as
                // "no such item", so tik4net leaves it as the generic trap.
                var error = Assert.Throws<TikCommandTrapException>(() => connection.ExecuteNonQuery(
                    "/user-manager/session/close-session", TikParam.Equal<SessionModel>(nameof(SessionModel.Id), "*FF")));

                error.Should().BeOfType<TikCommandTrapException>();
                error.Message.Should().NotBeNullOrWhiteSpace();

                output.WriteLine($"close-session of an unknown id | {error.GetType().Name}: {OneLine(error.Message)}");
            }
        }
        finally
        {
            await accounts.RemoveUsers(router, [username]);
        }

        (await UserAsync(handler, router, username)).Should().BeNull();
    }

    [Fact]
    public async Task ClosedPolicies_AreRejectedForTheTunnelUser()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<MikrotikInitializer>(VmKey, this);

        using var node = await scope.ServiceProvider.GetRequiredService<ISshHandler>().ConnectTo(Router());

        node.Execute("/tool ping 127.0.0.1 count=1", out var result).Should().BeFalse();
        result.Should().Contain("not enough permissions");

        node.Execute($"/user group set {PanelGroup} comment=tunnel-test", out result).Should().BeFalse();
        result.Should().Contain("not enough permissions");
    }

    [Fact]
    public async Task RouterOsErrorChannel_IsObserved()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<MikrotikInitializer>(VmKey, this);

        var router = Router();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<ServerBridgeOptions>>();
        var endpoint = SshEndpointFactory.Create(router, router.Config!, HostKeyPolicy.Required, options.Value);

        using var channel = await scope.ServiceProvider.GetRequiredService<ISshConnector>().ConnectAsync(endpoint, CancellationToken.None);
        using var commands = await channel.OpenCommandChannelAsync(CancellationToken.None);

        foreach (var command in RouterOsFailures)
        {
            var raw = commands.Execute(command, CommandTimeout);

            output.WriteLine($"RouterOS error channel | {command} | exit-status={raw.ExitStatus} stdout={OneLine(raw.Output)} stderr={OneLine(raw.Error)}");

            (raw.Output + raw.Error).Should().ContainAny("no such item", "not enough permissions", "bad command name");

            CommandOutcome.Classify(raw, OperatingSystem.Mikrotik).Success.Should().BeFalse();
        }

        // ...and it is the verdict the callers of the panel see
        using (var node = await scope.ServiceProvider.GetRequiredService<ISshHandler>().ConnectTo(router))
        {
            foreach (var command in RouterOsFailures)
            {
                node.Execute(command, out _).Should().BeFalse(command);
            }
        }
    }

    [Fact]
    public async Task PanelPrintCommands_AndWithoutPagingOrdering_AreObserved()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<MikrotikInitializer>(VmKey, this);

        var router = Router();
        var username = NewUsername();
        var accounts = scope.ServiceProvider.GetRequiredKeyedService<IInfraAccountRadiusSyncService>(RadiusType.UserManager);

        await accounts.SyncUserAndActive(
            router,
            new VpnAccountEntity { RealmId = 1, Username = username, VpnPassword = username },
            new RenewalEntity { SimultaneousUser = 1, TrafficLimit = 25 * StaticValues.BytesInGigLong, TimeLimitInDays = 10 });

        try
        {
            using var node = await scope.ServiceProvider.GetRequiredService<ISshHandler>().ConnectTo(router);

            node.Execute($"/user-manager user print where name=\"{username}\"", out var result).Should().BeTrue();
            result.Should().Contain(username);

            // RouterOS 7.20.6, same as 7.21.4: `where … without-paging` prints only the
            // header (rows=0) while `without-paging … where` returns the row. Recorded, not required.
            node.Execute($"/user-manager user print where name=\"{username}\" without-paging", out result).Should().BeTrue();
            output.WriteLine($"where … without-paging | rows={Rows(result)} | {OneLine(result)}");

            node.Execute($"/user-manager user print without-paging where name=\"{username}\"", out result).Should().BeTrue();
            output.WriteLine($"without-paging … where | rows={Rows(result)} | {OneLine(result)}");
        }
        finally
        {
            await accounts.RemoveUsers(router, [username]);
        }
    }

    [Fact]
    public async Task FaultedSession_AHeldCommandConnectionReportsTheLoss()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<MikrotikInitializer>(VmKey, this);

        var router = Router();
        var pool = Pool(scope);
        var session = await pool.GetAsync(router);
        var connection = await session.OpenCommandChannelAsync(CommandTimeout);

        try
        {
            pool.Fault(router.Id, session.Generation, "test: the session was lost under a held command connection");

            var error = (await Assert.ThrowsAsync<UserException>(
                () => Task.Run(() => connection.Execute(IdentityCommand, out _))))!;

            error.UserMessage.Should().Be(SshErrors.ConnectMessage);
        }
        finally
        {
            connection.Dispose();
        }

        // the next command connection runs on a session of the next generation
        using (var next = await scope.ServiceProvider.GetRequiredService<ISshHandler>().ConnectTo(router))
        {
            next.Execute(IdentityCommand, out var identity).Should().BeTrue(identity);
            identity.Should().Contain("name: MikroTik");
        }

        (await pool.GetAsync(router)).Generation.Should().Be(session.Generation + 1);
    }

    [Fact]
    public async Task ConnectionProbe_Discover_ReturnsTheRecordedPin_AndStopsBeforeAuth()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<MikrotikInitializer>(VmKey, this);

        var unpinned = Router(pin: "");

        await using (var channel = await scope.ServiceProvider.GetRequiredService<IServerChannelProbe>().ProbeAsync(unpinned))
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
        await scope.Register<MikrotikInitializer>(VmKey, this);

        var router = Router();
        var probe = scope.ServiceProvider.GetRequiredService<IServerChannelProbe>();

        await using (var channel = await probe.ProbeAsync(router))
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
                .GetRequiredKeyedService<IServiceProbe>(ServerFeature.UserManager)
                .ProbeAsync(router, channel.ForwardedLocalPort);

            service.Step.Should().Be(ConnectionStepModel.Service);
            service.Passed.Should().BeTrue(service.Detail);
            service.Detail.Should().Contain("MikroTik");
        }
    }

    [Fact]
    public async Task ConnectionProbe_Direct_WithoutAPin_IsAccepted_AndReachesTheApi()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<MikrotikInitializer>(VmKey, this);

        var direct = TestVmTunnel.DirectServer(VmKey, TestVmTunnel.MikrotikServerId + 1, ServerFeature.Radius);

        await using (var channel = await scope.ServiceProvider.GetRequiredService<IServerChannelProbe>().ProbeAsync(direct))
        {
            channel.Steps.Select(step => (step.Step, step.Passed)).Should().Equal(
                (ConnectionStepModel.Tcp, true),
                (ConnectionStepModel.HostKey, true),
                (ConnectionStepModel.Auth, true));

            channel.Steps[1].Detail.Should().Contain("بدون پین");

            // nothing is forwarded on the direct transport: the service is reached at its configured address
            channel.ForwardedLocalPort.Should().BeNull();

            var service = await scope.ServiceProvider
                .GetRequiredKeyedService<IServiceProbe>(ServerFeature.UserManager)
                .ProbeAsync(direct, channel.ForwardedLocalPort);

            service.Passed.Should().BeTrue(service.Detail);
        }
    }

    private static string NewUsername() => $"it.tunnel.{Guid.NewGuid().ToString("N")[..8]}";

    private static async Task<UserModel?> UserAsync(ITik4NetHandler handler, ServerEntity router, string username)
    {
        using var connection = await handler.ConnectTo(router);

        return connection.GetUser(username);
    }

    private static string Section(string ovpn, string name) =>
        Regex.Match(ovpn, $@"(?s)<{name}>\s*(.*?)\s*</{name}>").Groups[1].Value;

    private static string Der(string pem) => Regex.Replace(pem, @"-----[A-Z ]+-----|\s", string.Empty);

    private static string OneLine(string text) => text.Replace("\r", "\\r").Replace("\n", "\\n");

    private static int Rows(string printed) =>
        printed.Split('\n').Count(line => !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith('#') &&
                                           !line.TrimStart().StartsWith("Flags:") && !line.TrimStart().StartsWith("Columns:"));
}
