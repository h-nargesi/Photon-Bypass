using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.ServerBridge;
using PhotonBypass.ServerBridge.HostChecks;

namespace PhotonBypass.Test.Facts.BasicFunctions.ServerBridge;

/// <summary>
/// Host-capability checks G1–G3 (Docs/25 D11). These tests prove the logic
/// on loopback only: the real answer of the panel host comes with the first
/// publish (log lines, <c>POST /api/server/host-checks</c>).
/// </summary>
public class HostCapabilityChecksTest : IDisposable
{
    private readonly string temp_root = Path.Combine(Path.GetTempPath(), "host-checks-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task G1_OpenListenerPasses_ClosedPortFails()
    {
        var (listener, open) = Listen();
        var closed = ClosedPort();

        using var harness = CreateHarness(new ServerBridgeOptions { HostCheckTargets = [$"127.0.0.1:{open}", $"127.0.0.1:{closed}"] });

        var report = await harness.Checks.RunAsync();

        var passed = report.Targets.Single(target => target.Target == $"127.0.0.1:{open}");

        passed.Passed.Should().BeTrue();
        passed.Error.Should().BeNull();

        var failed = report.Targets.Single(target => target.Target == $"127.0.0.1:{closed}");

        failed.Passed.Should().BeFalse();
        failed.Error.Should().Be("ConnectionRefused");
        failed.Milliseconds.Should().BeLessOrEqualTo(5_000);

        report.OutboundTcp.Should().BeFalse("one failed destination fails G1 overall");
        report.Summary.Should().Be("host-check summary g1=fail g2=pass g3=observing targets=2");

        listener.Stop();
    }

    [Fact]
    public async Task G1_AllDestinationsReachable_Passes()
    {
        var (listener, open) = Listen();

        using var harness = CreateHarness(new ServerBridgeOptions { HostCheckTargets = [$"127.0.0.1:{open}"] });

        var report = await harness.Checks.RunAsync();

        report.OutboundTcp.Should().BeTrue();
        report.Summary.Should().Be("host-check summary g1=pass g2=pass g3=observing targets=1");

        listener.Stop();
    }

    [Fact]
    public async Task G2_BindsAListenerOnTheLoopbackAddress()
    {
        using var harness = CreateHarness();

        var report = await harness.Checks.RunAsync();

        report.LoopbackBind.Should().BeTrue();
        report.LoopbackPort.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task NoDestination_G1IsNotJudged()
    {
        using var harness = CreateHarness();

        var report = await harness.Checks.RunAsync();

        report.Targets.Should().BeEmpty();
        report.OutboundTcp.Should().BeNull("nothing was probed, so nothing passed or failed");
        report.Summary.Should().Be("host-check summary g1=no-targets g2=pass g3=observing targets=0");
    }

    [Fact]
    public async Task Destinations_ComeFromTheOptionsAndTheStoredRows_WithoutDuplicates()
    {
        var rows = new List<ServerEntity>
        {
            Row("127.0.0.1", 2201), // same endpoint as an option target: once
            Row("127.0.0.3", 2202),
            Row("127.0.0.4", ssh_config: false), // no ssh config: nothing to probe
            Row("127.0.0.5", port: 0), // out of range: skipped
        };

        using var harness = CreateHarness(new ServerBridgeOptions { HostCheckTargets = ["127.0.0.1:2201", "127.0.0.2:2201"] }, rows);

        var report = await harness.Checks.RunAsync();

        report.Targets.Select(target => target.Target).Should().BeEquivalentTo(
            ["127.0.0.1:2201", "127.0.0.2:2201", "127.0.0.3:2202"]);
    }

    [Fact]
    public async Task UnreadableStoredRows_StillProbeTheConfiguredTargets()
    {
        var repository = new Mock<IServerRepository>();

        repository.Setup(r => r.GetAllServers()).ThrowsAsync(new InvalidOperationException("database down"));

        using var services = new ServiceCollection().AddSingleton(repository.Object).BuildServiceProvider();
        using var checks = new HostCapabilityChecks(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new ServerBridgeOptions { HostCheckTargets = ["127.0.0.1:2210"] }),
            new FakeTimeProvider());

        var report = await checks.RunAsync();

        report.Targets.Should().ContainSingle().Which.Target.Should().Be("127.0.0.1:2210");
    }

    [Fact]
    public async Task Report_CarriesHostsPortsAndErrorKindsOnly()
    {
        const string secret = "private-key-and-password-of-the-server";

        var rows = new List<ServerEntity>
        {
            new()
            {
                Id = 1,
                Name = "R1",
                IpAddress = "127.0.0.9",
                DomainName = "r1",
                JsonConfig = JsonSerializer.Serialize(new ServerConfiguration
                {
                    SshConfig = new SshConfig { Username = "root", Password = secret, PrivateKey = secret, Port = 2199 },
                    WebApiConfig = new WebApiConfig { HostName = "127.0.0.9", Username = "admin", Password = secret },
                }),
            },
        };

        using var harness = CreateHarness(new ServerBridgeOptions(), rows);

        var report = await harness.Checks.RunAsync();

        var json = JsonSerializer.Serialize(report);

        json.Should().Contain("127.0.0.9:2199");
        json.Should().NotContain(secret);
        report.Uptime.Should().Be(TimeSpan.Zero, "the worker has not started in this harness");
    }

    [Fact]
    public void LogLines_FollowTheDocumentedShape()
    {
        HostCheckLogLines.Target(new HostCheckTargetResult
                { Target = "10.0.0.1:22", Passed = false, Milliseconds = 15, Error = "ConnectionRefused" })
            .Should().Be("host-check G1 tcp target=10.0.0.1:22 pass=false ms=15 error=ConnectionRefused");

        HostCheckLogLines.Target(new HostCheckTargetResult { Target = "10.0.0.1:22", Passed = true, Milliseconds = 3 })
            .Should().Be("host-check G1 tcp target=10.0.0.1:22 pass=true ms=3 error=none");

        HostCheckLogLines.LoopbackBind(true, 49152, null)
            .Should().Be("host-check G2 loopback-bind pass=true port=49152 error=none");

        HostCheckLogLines.LoopbackBind(false, 0, "SocketException")
            .Should().Be("host-check G2 loopback-bind pass=false port=0 error=SocketException");

        HostCheckLogLines.WorkerStarted(4242, "PANEL", "Microsoft Windows 10.0.20348")
            .Should().Be("host-check G3 worker-started pid=4242 machine=PANEL os=Microsoft Windows 10.0.20348");

        HostCheckLogLines.Heartbeat(new TimeSpan(1, 2, 3, 4, 500))
            .Should().Be("host-check G3 heartbeat uptime=1.02:03:04");

        HostCheckLogLines.Summary(null, true, 0).Should().Be("host-check summary g1=no-targets g2=pass g3=observing targets=0");
        HostCheckLogLines.Summary(true, true, 3).Should().Be("host-check summary g1=pass g2=pass g3=observing targets=3");
        HostCheckLogLines.Summary(false, false, 2).Should().Be("host-check summary g1=fail g2=fail g3=observing targets=2");
    }

    [Theory]
    [InlineData("host:port")]
    [InlineData("10.0.0.1")]
    [InlineData("10.0.0.1:0")]
    [InlineData("10.0.0.1:65536")]
    [InlineData("10.0.0.1 :22")]
    [InlineData("10.0.0.1:22:1")]
    [InlineData(" 10.0.0.1:22")]
    [InlineData("")]
    public void Target_InvalidShape_IsRejectedByTheOptionsValidation(string value)
    {
        HostCheckTarget.IsValid(value).Should().BeFalse();
    }

    [Theory]
    [InlineData("10.0.0.1:22")]
    [InlineData("server1.example:2222")]
    [InlineData("127.0.0.1:65535")]
    public void Target_ValidShape_IsAccepted(string value)
    {
        HostCheckTarget.TryParse(value, out var target).Should().BeTrue();
        target.ToString().Should().Be(value);
    }

    [Fact]
    public async Task StartAsync_ReturnsBeforeTheDestinationsAnswer()
    {
        // a documentation-reserved address no host answers: the probe can only end by its timeout
        using var harness = CreateHarness(new ServerBridgeOptions
        {
            HostCheckTargets = ["192.0.2.1:9"],
            SshConnectTimeoutSeconds = 3,
        });

        var watch = System.Diagnostics.Stopwatch.StartNew();

        await harness.Checks.StartAsync(CancellationToken.None);

        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2), "the probes run in the background");

        await harness.Checks.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task StopAsync_Twice_AndAfterDispose_IsANoOp()
    {
        // a host may stop after its run loop already did, and dispose before the second stop
        var checks = CreateHarness().Checks;

        await checks.StartAsync(CancellationToken.None);
        await checks.StopAsync(CancellationToken.None);

        checks.Dispose();

        var act = () => checks.StopAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("ServerBridgeOptions:HostCheckTargets:0", "no-port")]
    [InlineData("ServerBridgeOptions:HostCheckTargets:0", "10.0.0.1:70000")]
    [InlineData("ServerBridgeOptions:HostCheckHeartbeatMinutes", "0")]
    [InlineData("ServerBridgeOptions:SshTunnelIdleSeconds", "30")]
    [InlineData("ServerBridgeOptions:SshTunnelIdleSeconds", "86401")]
    public void InvalidOptions_FailTheStartupValidation(string key, string value)
    {
        using var host = BuildHost(new Dictionary<string, string?> { [key] = value });

        var act = () => host.Start();

        act.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void ValidOptions_StartTheHost()
    {
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["ServerBridgeOptions:HostCheckTargets:0"] = "127.0.0.1:22",
            ["ServerBridgeOptions:HostCheckTargets:1"] = "server1.example:2222",
        });

        var act = () => host.Start();

        act.Should().NotThrow();
    }

    private HarnessType CreateHarness(ServerBridgeOptions? options = null, List<ServerEntity>? rows = null)
    {
        var repository = new Mock<IServerRepository>();

        repository.Setup(r => r.GetAllServers()).ReturnsAsync(rows ?? []);

        var services = new ServiceCollection().AddSingleton(repository.Object).BuildServiceProvider();

        return new HarnessType(services, new HostCapabilityChecks(
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options ?? new ServerBridgeOptions()),
            new FakeTimeProvider()));
    }

    private IHost BuildHost(Dictionary<string, string?> values)
    {
        Directory.CreateDirectory(temp_root);

        var builder = new HostApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Production,
            ContentRootPath = temp_root,
        });

        builder.Configuration.AddInMemoryCollection(values);

        builder.AddServerBridgeServices();

        return builder.Build();
    }

    private static (TcpListener Listener, int Port) Listen()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);

        listener.Start();

        return (listener, ((IPEndPoint)listener.LocalEndpoint).Port);
    }

    private static int ClosedPort()
    {
        var (listener, port) = Listen();

        listener.Stop();

        return port;
    }

    private static ServerEntity Row(string ip, int port = 22, bool ssh_config = true) => new()
    {
        Id = ip.GetHashCode(),
        Name = "row",
        IpAddress = ip,
        DomainName = "row",
        JsonConfig = JsonSerializer.Serialize(new ServerConfiguration
        {
            SshConfig = ssh_config ? new SshConfig { Username = "root", Password = "x", Port = port } : null,
        }),
    };

    public void Dispose()
    {
        if (Directory.Exists(temp_root)) Directory.Delete(temp_root, recursive: true);
    }

    private sealed record HarnessType(IServiceProvider Services, HostCapabilityChecks Checks) : IDisposable
    {
        public void Dispose() => ((ServiceProvider)Services).Dispose();
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset started = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        public void Advance(TimeSpan span) => started += span;

        public override DateTimeOffset GetUtcNow() => started;
    }
}
