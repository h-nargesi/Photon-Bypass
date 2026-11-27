using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.ErrorHandler;
using PhotonBypass.ServerBridge;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.ServerBridge.Ssh;
using PhotonBypass.ServerBridge.Tunnel;
using PhotonBypass.Test.Mock.MockServerBridge;
using OperatingSystem = PhotonBypass.Domain.Servers.Types.OperatingSystem;

namespace PhotonBypass.Test.Facts.BasicFunctions.ServerBridge;

/// <summary>
/// The command connection over both transports (Docs/25 D3, D10): the direct
/// transport connects with a password on every call and still accepts a row
/// without key and pin; the tunnel transport borrows the pooled session; a
/// failed command returns false instead of throwing.
/// </summary>
public class SshHandlerTest
{
    private readonly FakeSshConnector connector = new();

    private SshHandler Handler(ServerBridgeOptions? settings = null, ISshTunnelPool? pool = null)
    {
        return new SshHandler(
            Options.Create(settings ?? TunnelFacts.DefaultOptions),
            connector,
            new Lazy<ISshTunnelPool>(() => pool ?? RealPool()));
    }

    private SshTunnelPool RealPool() => new(connector, Options.Create(TunnelFacts.DefaultOptions), TimeProvider.System);

    [Fact]
    public async Task ConnectTo_Direct_WithoutKeyAndPin_ConnectsWithThePasswordAndAnOptionalPin()
    {
        var server = TunnelFacts.Server(transport: ServerTransport.Direct, key: "", pin: "", passphrase: null);

        using var connection = await Handler().ConnectTo(server);

        connection.Server.Should().BeSameAs(server);

        var endpoint = connector.Endpoints.Should().ContainSingle().Subject;

        endpoint.Host.Should().Be("10.0.0.7");
        endpoint.Port.Should().Be(22);
        endpoint.Username.Should().Be("panel");
        endpoint.Password.Should().Be(TunnelFacts.Password);
        endpoint.PrivateKey.Should().BeNull();
        endpoint.HostKeySha256.Should().BeNull();
        endpoint.HostKeyPolicy.Should().Be(HostKeyPolicy.Optional);
        endpoint.ConnectTimeout.Should().Be(TunnelFacts.ConnectTimeout);
        endpoint.KeepAlive.Should().Be(TunnelFacts.KeepAlive);
    }

    [Fact]
    public async Task ConnectTo_Direct_AGivenPinIsPassedToTheConnector()
    {
        using (await Handler().ConnectTo(TunnelFacts.Server(transport: ServerTransport.Direct)))
        {
            connector.Connects.Should().Be(1);
        }

        connector.Endpoints.Single().HostKeySha256.Should().Be(TunnelFacts.PinnedKey);
        connector.Endpoints.Single().HostKeyPolicy.Should().Be(HostKeyPolicy.Optional);
    }

    [Fact]
    public async Task ConnectTo_Direct_OpensAFreshChannelPerCall_AndDisposesIt()
    {
        var server = TunnelFacts.Server(transport: ServerTransport.Direct);

        var handler = Handler();

        using (var first = await handler.ConnectTo(server))
        using (var second = await handler.ConnectTo(server))
        {
            first.Should().NotBeSameAs(second);
        }

        connector.Connects.Should().Be(2);
    }

    [Fact]
    public async Task ConnectTo_Tunnel_BorrowsThePooledSession_AndKeepsItAliveAfterTheCommand()
    {
        var server = TunnelFacts.Server();

        var pool = RealPool();
        var handler = Handler(pool: pool);

        using (var connection = await handler.ConnectTo(server))
        {
            connection.Execute("echo", out var result).Should().BeTrue();
            result.Should().BeEmpty();

            connector.Endpoints.Single().HostKeyPolicy.Should().Be(HostKeyPolicy.Required);
            connector.Endpoints.Single().HostKeySha256.Should().Be(TunnelFacts.PinnedKey);
            connector.Endpoints.Single().PrivateKey.Should().Be(TunnelFacts.KeyText);
            connector.Endpoints.Single().PassPhrase.Should().Be(TunnelFacts.PassPhrase);
        }

        // the direct transport owns its channel, the tunnel transport only cancels the command
        connector.LastChannel!.Disconnected.Should().BeFalse();
        connector.LastChannel.CommandChannel.CancelCalls.Should().Be(1);

        using (await handler.ConnectTo(server))
        {
            connector.Connects.Should().Be(1, "the pooled session serves every command connection");
        }

        pool.Dispose();
    }

    [Fact]
    public async Task Execute_CommandTimeoutComesFromTheOptions_OnBothTransports()
    {
        var settings = new ServerBridgeOptions { SshCommandTimeoutSeconds = 7 };

        using (var direct = await Handler(settings).ConnectTo(TunnelFacts.Server(transport: ServerTransport.Direct)))
        {
            direct.Execute("echo", out _);
        }

        using (var tunnel = await Handler(settings).ConnectTo(TunnelFacts.Server()))
        {
            tunnel.Execute("echo", out _);
        }

        connector.Endpoints.Select(endpoint => endpoint.HostKeyPolicy)
            .Should().Equal(HostKeyPolicy.Optional, HostKeyPolicy.Required);
        connector.Channels.SelectMany(channel => channel.CommandChannel.Timeouts)
            .Should().Equal(TimeSpan.FromSeconds(7), TimeSpan.FromSeconds(7));
    }

    [Theory]
    [InlineData(0, "", "ok")]
    [InlineData(-1, "", "name=\"CLIENT_user_1\"")]
    public async Task Execute_SuccessfulCommand_ReturnsTrueWithTheOutput(int exit_status, string error, string output)
    {
        var handler = Handler();

        using var connection = await handler.ConnectTo(TunnelFacts.Server(transport: ServerTransport.Direct));

        connector.LastChannel!.CommandChannel.Results.Enqueue(new SshCommandResult(exit_status, output, error));

        connection.Execute("print", out var result).Should().BeTrue();

        result.Should().Be(output);
        connector.LastChannel.CommandChannel.Commands.Should().Equal("print");
    }

    [Theory]
    [InlineData(0, "", "error on stderr")]
    [InlineData(1, "command failed", "")]
    public async Task Execute_FailedCommand_ReturnsFalseWithTheErrorText(int exit_status, string output, string error)
    {
        var handler = Handler();

        using var connection = await handler.ConnectTo(TunnelFacts.Server(transport: ServerTransport.Direct));

        connector.LastChannel!.CommandChannel.Results.Enqueue(new SshCommandResult(exit_status, output, error));

        connection.Execute("print", out var result).Should().BeFalse();

        result.Should().Be(error.Length > 0 ? error : output);
    }

    [Fact]
    public async Task Execute_RouterOsErrorText_ReturnsFalseWithTheOutput()
    {
        var server = TunnelFacts.Server(transport: ServerTransport.Direct);
        server.OsType = OperatingSystem.Mikrotik;

        using var connection = await Handler().ConnectTo(server);

        connector.LastChannel!.CommandChannel.Results.Enqueue(new SshCommandResult(-1, "no such item", ""));

        connection.Execute("/certificate sign", out var result).Should().BeFalse();

        result.Should().Be("no such item");
    }

    [Fact]
    public async Task Execute_Timeout_StillThrowsAUserException()
    {
        var handler = Handler();

        using var connection = await handler.ConnectTo(TunnelFacts.Server(transport: ServerTransport.Direct));

        connector.LastChannel!.CommandChannel.OnExecute = _ => throw new TimeoutException("timed out");

        var error = (await Assert.ThrowsAsync<UserException>(() => Task.Run(() => connection.Execute("sleep 100", out _))))!;

        error.UserMessage.Should().Be(SshErrors.CommandTimeoutMessage);
    }

    [Fact]
    public async Task Execute_LostSession_ThrowsAConnectUserException()
    {
        var handler = Handler();

        using var connection = await handler.ConnectTo(TunnelFacts.Server(transport: ServerTransport.Direct));

        connector.LastChannel!.CommandChannel.OnExecute = _ => throw new SshConnectException(
            SshConnectFailure.Connect, connector.Endpoints[0], step: "command");

        var error = (await Assert.ThrowsAsync<UserException>(() => Task.Run(() => connection.Execute("print", out _))))!;

        error.UserMessage.Should().Be(SshErrors.ConnectMessage);
    }

    [Fact]
    public async Task Execute_AfterDispose_IsRejected()
    {
        var connection = await Handler().ConnectTo(TunnelFacts.Server(transport: ServerTransport.Direct));

        connection.Dispose();

        var act = () => connection.Execute("print", out _);

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task Execute_Tunnel_LostSession_DropsThePooledSession_AndTheNextCommandReconnects()
    {
        var server = TunnelFacts.Server();
        var pool = RealPool();
        var handler = Handler(pool: pool);

        using (var connection = await handler.ConnectTo(server))
        {
            connector.LastChannel!.CommandChannel.OnExecute = _ => throw new SshConnectException(
                SshConnectFailure.Connect, connector.Endpoints[0], step: "command");

            var error = (await Assert.ThrowsAsync<UserException>(() => Task.Run(() => connection.Execute("print", out _))))!;

            error.UserMessage.Should().Be(SshErrors.ConnectMessage);
        }

        connector.LastChannel!.Disconnected.Should().BeTrue("the command path reports the loss like the forward paths");

        using var next = await handler.ConnectTo(server);

        connector.Connects.Should().Be(2);

        pool.Dispose();
    }

    [Fact]
    public async Task ConnectTo_Tunnel_CommandTimeoutBoundsTheWaitForTheSession()
    {
        var server = TunnelFacts.Server();
        var pool = RealPool();
        var handler = Handler(new ServerBridgeOptions { SshCommandTimeoutSeconds = 1 }, pool);

        using var first = await handler.ConnectTo(server);

        var watch = Stopwatch.StartNew();

        var error = (await Assert.ThrowsAsync<UserException>(() => handler.ConnectTo(server)))!;

        error.UserMessage.Should().Be(SshErrors.CommandTimeoutMessage);
        watch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));

        pool.Dispose();
    }

    [Theory]
    [InlineData("Authentication", SshErrors.AuthenticationMessage)]
    [InlineData("HostKeyMismatch", SshErrors.HostKeyMismatchMessage)]
    [InlineData("HostKeyMissing", SshErrors.HostKeyMissingMessage)]
    [InlineData("Connect", SshErrors.ConnectMessage)]
    public async Task ConnectTo_ARefusedConnect_LeavesAsAUserException_OnBothTransports(string name, string message)
    {
        var failure = Enum.Parse<SshConnectFailure>(name);

        connector.OnConnect = endpoint => throw new SshConnectException(failure, endpoint, TunnelFacts.PresentedKey);

        var pool = RealPool();
        var handler = Handler(pool: pool);

        foreach (var transport in new[] { ServerTransport.Direct, ServerTransport.SshTunnel })
        {
            var error = (await Assert.ThrowsAsync<UserException>(
                () => handler.ConnectTo(TunnelFacts.Server(transport: transport))))!;

            error.UserMessage.Should().Be(message);
        }

        connector.Endpoints.Select(endpoint => endpoint.HostKeyPolicy)
            .Should().Equal(HostKeyPolicy.Optional, HostKeyPolicy.Required);

        pool.Dispose();
    }

    [Fact]
    public async Task ConnectTo_WithoutAnSshConfiguration_KeepsTodaysError()
    {
        var server = TunnelFacts.Server(transport: ServerTransport.Direct);

        server.JsonConfig = JsonSerializer.Serialize(new ServerConfiguration());

        var error = await Assert.ThrowsAsync<Exception>(() => Handler().ConnectTo(server));

        error.Should().NotBeOfType<UserException>();
        error.Message.Should().Be("The ssh configuration is not set for server (7:T7)");
        connector.Connects.Should().Be(0);
    }

    [Fact]
    public async Task ConnectTo_Tunnel_WithoutAPersistedId_IsRejectedByThePool()
    {
        var pool = RealPool();

        var act = () => Handler(pool: pool).ConnectTo(TunnelFacts.Server(id: 0));

        await act.Should().ThrowAsync<ArgumentException>();

        pool.Dispose();
    }
}
