using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using PhotonBypass.Application.Management;
using PhotonBypass.Application.Management.Model;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Domain.Servers.Model;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockLocalRepository;
using PhotonBypass.Test.Mock.MockServerBridge;
using OperatingSystem = PhotonBypass.Domain.Servers.Types.OperatingSystem;
using System.Text.Json;

namespace PhotonBypass.Test.Facts.Application;

/// <summary>
/// The admin connection test (Docs/25 D8–D9) as an orchestrator over the two
/// probes (both mocked, wired through the real keyed registrations): what it
/// tests — the unsaved form or the stored row — when it stops, how the
/// service step is chosen and handed the forwarded port, the admin guard,
/// and that the answer carries no secret.
/// </summary>
public class ServerConnectionTesterTest : UnitLevelServiceInitializer
{
    private const string StoredConfig =
        """{"Transport":"Direct","SshConfig":{"Username":"root","Password":"stored-ssh-secret","Port":22},"WebApiConfig":{"HostName":"10.0.0.7","Port":8728,"Username":"admin","Password":"stored-api-secret"}}""";

    private const string DiscoveredKey = "SHA256:AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

    private readonly Mock<IServerChannelProbe> channel_probe = new();

    private readonly Mock<IServiceProbe> xui_probe = new();

    private readonly Mock<IServiceProbe> user_manager_probe = new();

    private bool channel_released;

    protected override void AddClassTestServices(IHostApplicationBuilder builder)
    {
        // registered last: they win over the real adapters of the host
        builder.Services.AddScoped<IServerChannelProbe>(_ => channel_probe.Object);
        Func<IServiceProvider, object?, IServiceProbe> xui = (_, _) => xui_probe.Object;
        Func<IServiceProvider, object?, IServiceProbe> user_manager = (_, _) => user_manager_probe.Object;

        builder.Services.AddKeyedTransient<IServiceProbe>(ServerFeature.Xui, xui);
        builder.Services.AddKeyedTransient<IServiceProbe>(ServerFeature.UserManager, user_manager);
    }

    [Fact]
    public async Task StopsAtTheFirstFailedStep_AndNeverAsksTheService()
    {
        using var context = Admin();
        Channel(
        [
            ConnectionStepModel.Pass(ConnectionStepModel.Tcp),
            ConnectionStepModel.Fail(ConnectionStepModel.HostKey, "mismatch"),
        ]);

        var report = await context.Tester.TestAsync(Model());

        report.Passed.Should().BeFalse();
        report.Steps.Select(step => step.Step).Should().Equal(ConnectionStepModel.Tcp, ConnectionStepModel.HostKey);
        xui_probe.Verify(p => p.ProbeAsync(It.IsAny<ServerEntity>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
        user_manager_probe.Verify(p => p.ProbeAsync(It.IsAny<ServerEntity>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TunnelWithAnEmptyPin_StopsBeforeAuth_AndReportsTheFingerprintToRegister()
    {
        using var context = Admin();

        // what the channel probe reports for a discovery connect: tcp, the discovered key, and auth stopped on purpose
        Channel(
        [
            ConnectionStepModel.Pass(ConnectionStepModel.Tcp),
            ConnectionStepModel.Pass(ConnectionStepModel.HostKey, "کشف شد"),
            ConnectionStepModel.Fail(ConnectionStepModel.Auth, "pin not registered"),
        ], host_key: DiscoveredKey);

        var report = await context.Tester.TestAsync(Model());

        report.Passed.Should().BeFalse("nothing was authenticated");
        report.HostKeySha256.Should().Be(DiscoveredKey);
        report.Steps[^1].Step.Should().Be(ConnectionStepModel.Auth);
        xui_probe.Verify(p => p.ProbeAsync(It.IsAny<ServerEntity>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DirectWithAnEmptyPin_ContinuesToTheService()
    {
        using var context = Admin();

        Channel(
        [
            ConnectionStepModel.Pass(ConnectionStepModel.Tcp),
            ConnectionStepModel.Pass(ConnectionStepModel.HostKey, "بدون پین"),
            ConnectionStepModel.Pass(ConnectionStepModel.Auth),
        ]);
        Service(xui_probe, ConnectionStepModel.Pass(ConnectionStepModel.Service, "xui"));

        var report = await context.Tester.TestAsync(Model());

        report.Passed.Should().BeTrue();
        report.Steps.Select(step => step.Step).Should().Equal(
            ConnectionStepModel.Tcp, ConnectionStepModel.HostKey, ConnectionStepModel.Auth, ConnectionStepModel.Service);
    }

    [Fact]
    public async Task Tunnel_HandsTheForwardedPortToTheServiceStep()
    {
        using var context = Admin();

        Channel(AllChannelSteps(), forwarded_local_port: 41_001);
        Service(xui_probe, ConnectionStepModel.Pass(ConnectionStepModel.Service, "xui"));

        await context.Tester.TestAsync(Model());

        xui_probe.Verify(p => p.ProbeAsync(It.IsAny<ServerEntity>(), 41_001, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Direct_HandsNoForwardedPortToTheServiceStep()
    {
        using var context = Admin();

        Channel(AllChannelSteps());
        Service(xui_probe, ConnectionStepModel.Pass(ConnectionStepModel.Service, "xui"));

        await context.Tester.TestAsync(Model());

        xui_probe.Verify(p => p.ProbeAsync(It.IsAny<ServerEntity>(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(ServerFeature.Xui | ServerFeature.UserManager | ServerFeature.V2ray, "xui")]
    [InlineData(ServerFeature.UserManager | ServerFeature.V2ray, "user-manager")]
    [InlineData(ServerFeature.Xui | ServerFeature.V2ray, "xui")]
    public async Task ServiceStep_IsChosenByTheRadiusFeature(ServerFeature features, string expected)
    {
        using var context = Admin();

        Channel(AllChannelSteps());
        Service(xui_probe, ConnectionStepModel.Pass(ConnectionStepModel.Service, "xui"));

        var report = await context.Tester.TestAsync(Model(features: features));

        report.Passed.Should().BeTrue();
        report.Steps[^1].Detail.Should().Be(expected);
    }

    [Fact]
    public async Task ServerWithoutARadiusFeature_HasNoServiceStep()
    {
        using var context = Admin();

        Channel(AllChannelSteps());

        var report = await context.Tester.TestAsync(Model(features: ServerFeature.Nas | ServerFeature.V2ray));

        report.Passed.Should().BeTrue();
        report.Steps.Should().HaveCount(3);
    }

    [Fact]
    public async Task FailedServiceStep_FailsTheReport()
    {
        using var context = Admin();

        Channel(AllChannelSteps());
        Service(xui_probe, ConnectionStepModel.Fail(ConnectionStepModel.Service, "rejected"));

        var report = await context.Tester.TestAsync(Model());

        report.Passed.Should().BeFalse();
        report.Steps[^1].Step.Should().Be(ConnectionStepModel.Service);
    }

    [Fact]
    public async Task UnsavedForm_ItsConfigAndFieldsReachTheProbes_NoStoredRowIsRead()
    {
        using var context = Admin();

        Channel(AllChannelSteps());
        Service(xui_probe, ConnectionStepModel.Pass(ConnectionStepModel.Service, "xui"));

        await context.Tester.TestAsync(Model(config: TunnelFacts.Server().Config, ip: " 10.0.0.9 "));

        var tested = Tested();

        tested.Id.Should().Be(0);
        tested.IpAddress.Should().Be("10.0.0.9");
        tested.Features.Should().Be(ServerFeature.Xui | ServerFeature.V2ray);
        tested.Config!.Transport.Should().Be(ServerTransport.SshTunnel);
        tested.Config.SshConfig!.HostKeySha256.Should().Be(TunnelFacts.PinnedKey);
    }

    [Fact]
    public async Task RowAction_TestsTheStoredRowAsItIs()
    {
        using var context = Admin();
        var stored = context.Stored();

        Channel(AllChannelSteps());
        Service(xui_probe, ConnectionStepModel.Pass(ConnectionStepModel.Service, "xui"));

        await context.Tester.TestAsync(new TestServerConnectionModel { Id = stored.Id });

        var tested = Tested();

        tested.IpAddress.Should().Be(stored.IpAddress);
        tested.Name.Should().Be(stored.Name);
        tested.Features.Should().Be(stored.Features);
        tested.OsType.Should().Be(stored.OsType);
        tested.JsonConfig.Should().Be(stored.JsonConfig, "the protected blob is handed over untouched");
    }

    [Fact]
    public async Task EditForm_WithAnEmptyConfigBox_TestsTheStoredConfigAtTheNewAddress()
    {
        using var context = Admin();
        context.Stored();

        Channel(AllChannelSteps());
        Service(xui_probe, ConnectionStepModel.Pass(ConnectionStepModel.Service, "xui"));

        await context.Tester.TestAsync(new TestServerConnectionModel { Id = 7, IpAddress = " 10.0.0.9 " });

        var tested = Tested();

        tested.IpAddress.Should().Be("10.0.0.9");
        tested.JsonConfig.Should().Be(StoredConfig);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(0)]
    public async Task NoConfigurationAnywhere_IsAUserMessage(int id)
    {
        using var context = Admin();
        context.Stored().JsonConfig = null;

        var error = (await Assert.ThrowsAsync<UserException>(() => context.Tester.TestAsync(new TestServerConnectionModel { Id = id })))!;

        error.UserMessage.Should().Be("پیکربندی سرور برای تست در دسترس نیست!");
        channel_probe.Verify(p => p.ProbeAsync(It.IsAny<ServerEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UnknownId_ReportsTheMissingRow()
    {
        using var context = Admin();

        var error = (await Assert.ThrowsAsync<UserException>(() => context.Tester.TestAsync(new TestServerConnectionModel { Id = 999 })))!;

        error.UserMessage.Should().Be("سرور پیدا نشد!");
    }

    [Fact]
    public async Task NegativeId_IsRejected()
    {
        using var context = Admin();

        var error = (await Assert.ThrowsAsync<UserException>(() => context.Tester.TestAsync(new TestServerConnectionModel { Id = -3 })))!;

        error.UserMessage.Should().Be("شناسه سرور نامعتبر است!");
    }

    [Theory]
    [InlineData(AccountRole.User)]
    [InlineData(AccountRole.Manager)]
    public async Task NonAdmin_IsRefusedBeforeAnythingIsProbed(AccountRole role)
    {
        using var context = Admin(role);

        var error = (await Assert.ThrowsAsync<UserException>(
            () => context.Tester.TestAsync(Model(config: TunnelFacts.Server().Config))))!;

        error.HttpCode.Should().Be(403);
        channel_probe.Verify(p => p.ProbeAsync(It.IsAny<ServerEntity>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Report_CarriesStepsFingerprintAndMachineOnly()
    {
        using var context = Admin();

        Channel(AllChannelSteps(), host_key: DiscoveredKey);
        Service(xui_probe, ConnectionStepModel.Pass(ConnectionStepModel.Service, "x-ui"));

        var report = await context.Tester.TestAsync(Model(config: TunnelFacts.Server().Config));

        var json = JsonSerializer.Serialize(report);

        json.Should().Contain(DiscoveredKey).And.Contain(Environment.MachineName);
        json.Should().NotContainAny(TunnelFacts.KeyText, TunnelFacts.PassPhrase, TunnelFacts.Password, "api-secret", "x-ui=");
    }

    [Fact]
    public async Task TheChannelResult_IsReleasedWhenTheTestIsOver()
    {
        using var context = Admin();

        Channel(AllChannelSteps());
        Service(xui_probe, ConnectionStepModel.Pass(ConnectionStepModel.Service, "xui"));

        channel_released.Should().BeFalse("the session stays open until the service step went through its forward");

        await context.Tester.TestAsync(Model());

        channel_released.Should().BeTrue();
    }

    private Context Admin(AccountRole role = AccountRole.Admin)
    {
        var scope = App.Services.CreateScope();

        var accounts = scope.ServiceProvider.GetRequiredService<PanelUserRepositoryMoq>();
        var servers = scope.ServiceProvider.GetRequiredService<ServerRepositoryMoq>();

        accounts.Data["User99"].Role = role;
        scope.ServiceProvider.GetRequiredService<IJobContext>().InjectJobContext(99, "User99", "User99");

        user_manager_probe.Setup(p => p.ProbeAsync(It.IsAny<ServerEntity>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => ConnectionStepModel.Pass(ConnectionStepModel.Service, "user-manager"));

        return new Context(scope, scope.ServiceProvider.GetRequiredService<IServerConnectionTester>(), servers);
    }

    private void Channel(ConnectionStepModel[] steps, string? host_key = null, int? forwarded_local_port = null)
    {
        channel_released = false;

        var result = new ChannelProbeResult([.. steps], host_key, forwarded_local_port,
            () =>
            {
                channel_released = true;

                return ValueTask.CompletedTask;
            });

        channel_probe.Setup(p => p.ProbeAsync(It.IsAny<ServerEntity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    private static void Service(Mock<IServiceProbe> probe, ConnectionStepModel step)
    {
        probe.Setup(p => p.ProbeAsync(It.IsAny<ServerEntity>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(step);
    }

    private ServerEntity Tested() =>
        channel_probe.Invocations.Single().Arguments[0].Should().BeOfType<ServerEntity>().Subject;

    private static ConnectionStepModel[] AllChannelSteps() =>
    [
        ConnectionStepModel.Pass(ConnectionStepModel.Tcp),
        ConnectionStepModel.Pass(ConnectionStepModel.HostKey),
        ConnectionStepModel.Pass(ConnectionStepModel.Auth),
    ];

    private static TestServerConnectionModel Model(
        ServerFeature features = ServerFeature.Xui | ServerFeature.V2ray,
        ServerConfiguration? config = null, string ip = "10.0.0.7")
    {
        return new TestServerConnectionModel
        {
            Name = "SRV-Test",
            IpAddress = ip,
            Features = features,
            OsType = OperatingSystem.Ubuntu,
            Config = config ?? TunnelFacts.Server(transport: ServerTransport.Direct).Config,
        };
    }

    [Fact]
    public async Task AnIncompleteForm_IsTestedToo()
    {
        using var context = Admin();
        Channel(AllChannelSteps());

        // no realm, no capacity, no os, no SSH password: the test is not a save
        await context.Tester.TestAsync(new TestServerConnectionModel
        {
            IpAddress = "10.0.0.7",
            Config = new ServerConfiguration { Transport = ServerTransport.SshTunnel, SshConfig = new SshConfig { Username = "root", Port = 22 } },
        });

        var tested = Tested();

        tested.Id.Should().Be(0);
        tested.IpAddress.Should().Be("10.0.0.7");
        tested.Features.Should().Be(0, "no Radius feature: no service step");
        tested.Config!.SshConfig!.Password.Should().BeNull("a tunnel row carries no SSH password");
    }

    private sealed record Context(IServiceScope Scope, IServerConnectionTester Tester, ServerRepositoryMoq Servers) : IDisposable
    {
        public ServerEntity Stored()
        {
            var stored = new ServerEntity
            {
                Id = 7,
                Name = "SRV-Stored",
                IpAddress = "10.0.0.7",
                DomainName = "stored",
                OsType = OperatingSystem.Mikrotik,
                Features = ServerFeature.Xui | ServerFeature.V2ray,
                JsonConfig = StoredConfig,
            };

            Servers.Data[stored.Id] = stored;

            return stored;
        }

        public void Dispose() => Scope.Dispose();
    }
}
