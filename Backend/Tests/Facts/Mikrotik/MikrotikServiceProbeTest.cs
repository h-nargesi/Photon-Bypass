using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Moq;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Domain.Servers.Model;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.ServerBridge.Tik4net;
using PhotonBypass.Test.Mock.MockServerBridge;
using PhotonBypass.Test.Initializer;
using tik4net;

namespace PhotonBypass.Test.Facts.Mikrotik;

/// <summary>
/// The service step of the connection test for a User Manager server
/// (Docs/25 D9): the RouterOS API opened on the configured host or on the
/// test's own forwarded port, followed by one read command. tik4net is mocked.
/// </summary>
public class MikrotikServiceProbeTest : UnitLevelServiceInitializer
{
    private readonly Mock<ITikConnection> connection = new();

    private readonly Mock<ITikCommand> command = new();

    protected override void AddClassTestServices(IHostApplicationBuilder builder)
    {
        builder.Configuration["ServerBridgeOptions:Tik4NetOpenTimeoutSeconds"] = "1";

        builder.Services.RemoveAll<TikConnectionFactory>();
        builder.Services.AddSingleton<TikConnectionFactory>(_ => connection.Object);

        connection.Setup(c => c.OpenAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        connection.Setup(c => c.CreateCommand(It.IsAny<string>())).Returns(command.Object);

        command.Setup(c => c.ExecuteScalar(It.IsAny<string>())).Returns("MikroTik-Core");
    }

    [Fact]
    public async Task Direct_OpensTheConfiguredHost_AndReadsTheIdentity()
    {
        using var scope = App.Services.CreateScope();

        var step = await Probe(scope).ProbeAsync(Server(ServerTransport.Direct), null);

        step.Step.Should().Be(ConnectionStepModel.Service);
        step.Passed.Should().BeTrue();
        step.Detail.Should().Contain("User Manager").And.Contain("MikroTik-Core");

        Opened().Should().ContainSingle().Which.Should().Be(("10.0.0.7", 8728, "admin", "api-secret"));
        command.Verify(c => c.ExecuteScalar("name"), Times.Once);
        connection.Verify(c => c.CreateCommand("/system/identity/print"), Times.Once);
        connection.Verify(c => c.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Tunnel_OpensTheForwardedLoopbackPort()
    {
        using var scope = App.Services.CreateScope();

        var step = await Probe(scope).ProbeAsync(Server(ServerTransport.SshTunnel), 41_001);

        step.Passed.Should().BeTrue();
        Opened().Single().Should().Be(("127.0.0.1", 41_001, "admin", "api-secret"));
    }

    [Fact]
    public async Task LoginFailure_IsNotATransportFailure()
    {
        connection.Setup(c => c.OpenAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new TikConnectionLoginException(new Exception("invalid user name or password")));

        using var scope = App.Services.CreateScope();

        var step = await Probe(scope).ProbeAsync(Server(ServerTransport.SshTunnel), 41_001);

        step.Passed.Should().BeFalse();
        step.Detail.Should().Be("اعتبارنامه‌ی User Manager رد شد!");
        connection.Verify(c => c.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Timeout_IsNotATransportFailure()
    {
        connection.Setup(c => c.OpenAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(new TaskCompletionSource().Task);

        using var scope = App.Services.CreateScope();

        var step = await Probe(scope).ProbeAsync(Server(ServerTransport.Direct), null);

        step.Detail.Should().Be("پاسخی از User Manager دریافت نشد!");
    }

    [Theory]
    [InlineData(ServerTransport.Direct, "ارتباط با User Manager برقرار نشد!")]
    [InlineData(ServerTransport.SshTunnel, "ورود به User Manager ممکن نشد!")]
    public async Task OpenFailure_IsReportedWithItsTransportSpecificWording(ServerTransport transport, string expected)
    {
        connection.Setup(c => c.OpenAsync(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new IOException("connection closed"));

        using var scope = App.Services.CreateScope();

        var step = await Probe(scope).ProbeAsync(Server(transport), transport == ServerTransport.SshTunnel ? 41_001 : null);

        step.Passed.Should().BeFalse();
        step.Detail.Should().StartWith(expected);
    }

    [Fact]
    public async Task WithoutAWebConfiguration_FailsWithoutOpeningAnything()
    {
        var server = Server(ServerTransport.Direct);

        server.JsonConfig = """{"Transport":"Direct"}""";

        using var scope = App.Services.CreateScope();

        var step = await Probe(scope).ProbeAsync(server, null);

        step.Passed.Should().BeFalse();
        step.Detail.Should().Contain("User Manager");
        connection.Verify(c => c.OpenAsync(
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void EachRadiusFeature_HasItsOwnServiceProbe()
    {
        using var scope = App.Services.CreateScope();

        Probe(scope).GetType().Name.Should().Be("MikrotikServiceProbe");
        scope.ServiceProvider.GetRequiredKeyedService<IServiceProbe>(ServerFeature.Xui).GetType().Name.Should().Be("XuiServiceProbe");
    }

    private static IServiceProbe Probe(IServiceScope scope)
    {
        return scope.ServiceProvider.GetRequiredKeyedService<IServiceProbe>(ServerFeature.UserManager);
    }

    private static ServerEntity Server(ServerTransport transport)
    {
        return TunnelFacts.Server(
            transport: transport,
            features: ServerFeature.UserManager | ServerFeature.V2ray,
            web: new WebApiConfig
            {
                HostName = transport == ServerTransport.SshTunnel ? "127.0.0.1" : "10.0.0.7",
                Port = 8728,
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
