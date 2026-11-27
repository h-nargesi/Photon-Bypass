using FluentAssertions;
using Microsoft.Extensions.Options;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.ErrorHandler;
using PhotonBypass.ServerBridge;
using PhotonBypass.ServerBridge.Tunnel;
using PhotonBypass.Test.Mock.MockServerBridge;

namespace PhotonBypass.Test.Facts.BasicFunctions.ServerBridge;

/// <summary>
/// The pooled SSH sessions of the tunnel transport (Docs/25 D5) against the
/// fake connector: one connect per server, reconnect after a fault with a
/// higher generation, rebuild on a changed SSH leg, the idle sweep, and the
/// user-facing wording of every connect failure.
/// </summary>
public class SshTunnelPoolTest : IDisposable
{
    private readonly FakeSshConnector connector = new();

    private readonly FakeTimeProvider time = new();

    private readonly SshTunnelPool pool;

    public SshTunnelPoolTest()
    {
        pool = new SshTunnelPool(
            connector,
            Options.Create(new ServerBridgeOptions { SshTunnelIdleSeconds = 60 }),
            time);
    }

    public void Dispose() => pool.Dispose();

    [Fact]
    public async Task GetAsync_Twice_ConnectsOnce_AndHandsOutTheSameSession()
    {
        var server = TunnelFacts.Server();

        var first = await pool.GetAsync(server);
        var second = await pool.GetAsync(server);

        connector.Connects.Should().Be(1);
        second.Should().BeSameAs(first);
        first.Generation.Should().Be(1);
    }

    [Fact]
    public async Task GetAsync_ConcurrentCallers_ShareOneConnection()
    {
        var server = TunnelFacts.Server();

        var sessions = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => pool.GetAsync(server).AsTask()));

        connector.Connects.Should().Be(1);
        sessions.Should().OnlyContain(session => ReferenceEquals(session, sessions[0]));
        sessions[0].Generation.Should().Be(1);
    }

    [Fact]
    public async Task StopAsync_Twice_AndAfterDispose_IsANoOp()
    {
        // a host may stop after its run loop already did, and dispose before the second stop
        await pool.StartAsync(CancellationToken.None);
        await pool.StopAsync(CancellationToken.None);

        pool.Dispose();

        var act = () => pool.StopAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Fault_DropsTheSession_TheNextGetReconnectsWithTheNextGeneration()
    {
        var server = TunnelFacts.Server();

        var first = await pool.GetAsync(server);
        var channel = connector.LastChannel!;

        pool.Fault(server.Id, first.Generation, "transport failure");

        channel.Disconnected.Should().BeTrue();

        var second = await pool.GetAsync(server);

        connector.Connects.Should().Be(2);
        second.Should().NotBeSameAs(first);
        second.Generation.Should().Be(2);
    }

    [Fact]
    public async Task FaultForAnOldGeneration_LeavesTheReplacementAlive()
    {
        var server = TunnelFacts.Server();

        var first = await pool.GetAsync(server);
        var old_channel = connector.LastChannel!;

        pool.Fault(server.Id, first.Generation, "transport failure");

        var second = await pool.GetAsync(server);

        // the consumer that failed on the old session reports it only after the replacement exists
        pool.Fault(server.Id, first.Generation, "late report of the old session");
        pool.Fault(server.Id, first.Generation + 5, "a generation that never existed");

        old_channel.Disconnected.Should().BeTrue();
        connector.LastChannel!.Disconnected.Should().BeFalse("the replacement is not the session that failed");
        (await pool.GetAsync(server)).Should().BeSameAs(second);
        connector.Connects.Should().Be(2);
    }

    [Fact]
    public async Task ASessionErrorReportedLate_LeavesTheReplacementAlive()
    {
        var server = TunnelFacts.Server();

        var first = (SshTunnelPool.Session)await pool.GetAsync(server);
        var old_channel = connector.LastChannel!;

        // the error event of the old session is disposed on the thread pool, so its report may run
        // after a consumer already reconnected — replay exactly that order
        pool.Fault(server.Id, first.Generation, "the consumer noticed the loss");
        var second = await pool.GetAsync(server);

        pool.Fault(first, "session error (IOException)");

        old_channel.Disconnected.Should().BeTrue();
        connector.LastChannel!.Disconnected.Should().BeFalse("a report of generation 1 never closes generation 2");
        (await pool.GetAsync(server)).Should().BeSameAs(second);
        connector.Connects.Should().Be(2);
    }

    [Fact]
    public async Task ASessionError_DropsExactlyTheSessionThatRaisedIt()
    {
        var server = TunnelFacts.Server();

        await pool.GetAsync(server);

        var channel = connector.LastChannel!;
        channel.RaiseSessionError(new IOException("connection reset"));

        await UntilAsync(() => channel.Disconnected);

        connector.Connects.Should().Be(1, "nothing reconnects on its own");
        (await pool.GetAsync(server)).Generation.Should().Be(2);
    }

    [Fact]
    public async Task Fault_ForAnUnknownServer_IsIgnored()
    {
        var act = () => pool.Fault(404, 1, "nothing pooled yet");

        act.Should().NotThrow();
    }

    [Fact]
    public async Task SessionError_DropsTheSession_LikeAnExplicitFault()
    {
        var server = TunnelFacts.Server();

        await pool.GetAsync(server);

        var channel = connector.LastChannel!;

        channel.RaiseSessionError(new IOException("connection reset"));

        await UntilAsync(() => channel.Disconnected);

        (await pool.GetAsync(server)).Generation.Should().Be(2);
        connector.Connects.Should().Be(2);
    }

    [Fact]
    public async Task DisconnectedSession_NotYetReported_IsReplacedOnTheNextGet()
    {
        var server = TunnelFacts.Server();

        await pool.GetAsync(server);

        // the keep-alive noticed the loss but the event has not arrived yet
        connector.LastChannel!.IsConnected = false;

        (await pool.GetAsync(server)).Generation.Should().Be(2);
        connector.Connects.Should().Be(2);
    }

    [Theory]
    [InlineData("key")]
    [InlineData("pin")]
    [InlineData("port")]
    [InlineData("username")]
    public async Task ChangedSshLeg_RebuildsTheSession(string changed)
    {
        var before = TunnelFacts.Server();

        await pool.GetAsync(before);

        var old_channel = connector.LastChannel!;

        var after = changed switch
        {
            "key" => TunnelFacts.Server(key: TunnelFacts.OtherKeyText),
            "pin" => TunnelFacts.Server(pin: TunnelFacts.PresentedKey),
            "port" => TunnelFacts.Server(ssh_port: 2222),
            _ => TunnelFacts.Server(username: "other"),
        };

        var session = await pool.GetAsync(after);

        connector.Connects.Should().Be(2);
        old_channel.Disconnected.Should().BeTrue();
        session.Generation.Should().Be(2);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("passphrase")]
    [InlineData("web")]
    public async Task UnrelatedChanges_KeepTheLiveSession(string changed)
    {
        await pool.GetAsync(TunnelFacts.Server());

        var after = changed switch
        {
            "password" => TunnelFacts.Server(password: "changed-ssh-password"),
            "passphrase" => TunnelFacts.Server(passphrase: "changed-pass-phrase"),
            _ => TunnelFacts.Server(web: new WebApiConfig { HostName = "127.0.0.1", Port = 2087 }),
        };

        var session = await pool.GetAsync(after);

        connector.Connects.Should().Be(1, "only host, port, username, pin and key identify the SSH leg");
        session.Generation.Should().Be(1);
    }

    [Fact]
    public async Task ForwardedPort_IsStablePerRemotePort_AndDistinctAcrossPorts()
    {
        var server = TunnelFacts.Server();

        var session = await pool.GetAsync(server);

        var first = session.ForwardedPort(2053);
        var again = session.ForwardedPort(2053);
        var second = session.ForwardedPort(8728);

        again.Should().Be(first);
        second.Should().NotBe(first);
        connector.LastChannel!.StartForwardCalls.Should().Be(2, "the second ask for the same port reuses its forward");
    }

    [Fact]
    public async Task ForwardedPort_OnAFaultedSession_Reconnects()
    {
        var server = TunnelFacts.Server();

        var first = await pool.GetAsync(server);
        var old_channel = connector.LastChannel!;

        first.ForwardedPort(2053);

        pool.Fault(server.Id, first.Generation, "transport failure");

        var second = await pool.GetAsync(server);

        second.ForwardedPort(2053);

        second.Should().NotBeSameAs(first);
        old_channel.StartForwardCalls.Should().Be(1);
        connector.LastChannel.Should().NotBeSameAs(old_channel);
        connector.LastChannel!.StartForwardCalls.Should().Be(1, "the new session opens its own forward for the port");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void GetAsync_WithoutAPersistedId_IsRejectedBeforeAnyConnect(int id)
    {
        var act = () => pool.GetAsync(TunnelFacts.Server(id: id)).AsTask();

        act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*ServerEntity.Id*");
        connector.Connects.Should().Be(0);
    }

    [Fact]
    public async Task Sweep_EvictsAnIdleSession_TheNextGetReconnects()
    {
        var server = TunnelFacts.Server();

        await pool.GetAsync(server);

        var channel = connector.LastChannel!;

        pool.Sweep().Should().Be(0, "the session was just used");
        channel.Disconnected.Should().BeFalse();

        time.Advance(TimeSpan.FromSeconds(61));

        pool.Sweep().Should().Be(1);
        channel.Disconnected.Should().BeTrue();
        pool.Sweep().Should().Be(0, "an evicted session is not swept twice");

        (await pool.GetAsync(server)).Generation.Should().Be(2);
    }

    [Fact]
    public async Task Sweep_KeepsASessionThatIsStillInUse()
    {
        var server = TunnelFacts.Server();

        await pool.GetAsync(server);

        time.Advance(TimeSpan.FromSeconds(50));
        await pool.GetAsync(server); // touch
        time.Advance(TimeSpan.FromSeconds(50));

        pool.Sweep().Should().Be(0, "the idle lifetime counts from the last use");
        connector.LastChannel!.Disconnected.Should().BeFalse();
    }

    [Theory]
    [InlineData("HostKeyMismatch", SshErrors.HostKeyMismatchMessage)]
    [InlineData("HostKeyMissing", SshErrors.HostKeyMissingMessage)]
    [InlineData("Authentication", SshErrors.AuthenticationMessage)]
    [InlineData("KeyInvalid", SshErrors.KeyInvalidMessage)]
    [InlineData("Timeout", SshErrors.ConnectMessage)]
    [InlineData("Connect", SshErrors.ConnectMessage)]
    public async Task ConnectFailures_LeaveAsUserExceptionsWithoutSecrets(string name, string message)
    {
        var failure = Enum.Parse<SshConnectFailure>(name);

        connector.OnConnect = endpoint => throw new SshConnectException(
            failure, endpoint, TunnelFacts.PresentedKey, "authentication", 1500,
            new Exception($"inner detail {TunnelFacts.KeyText} {TunnelFacts.PassPhrase} {TunnelFacts.Password}"));

        var error = (await Assert.ThrowsAsync<UserException>(() => pool.GetAsync(TunnelFacts.Server()).AsTask()))!;

        error.UserMessage.Should().Be(message);
        error.ToString().Should().NotContainAny(TunnelFacts.KeyText, TunnelFacts.PassPhrase, TunnelFacts.Password, "api-secret");
    }

    [Fact]
    public async Task ConnectFailures_KeepHostAndFingerprintsInTheDeveloperDetail()
    {
        connector.OnConnect = endpoint => throw new SshConnectException(
            SshConnectFailure.HostKeyMismatch, endpoint, TunnelFacts.PresentedKey);

        var error = (await Assert.ThrowsAsync<UserException>(() => pool.GetAsync(TunnelFacts.Server()).AsTask()))!;

        error.Message.Should().Contain("10.0.0.7:22")
            .And.Contain(TunnelFacts.PresentedKey)
            .And.Contain(TunnelFacts.PinnedKey);
    }

    [Theory]
    [InlineData("username", "برای حالت تانل، کاربر SSH الزامی است!")]
    [InlineData("key", "کلید خصوصی SSH معتبر نیست!")]
    public async Task TunnelRow_WithoutWhatTheSshLegNeeds_IsRefusedWithTheAdminWording(string missing, string message)
    {
        var server = missing == "username"
            ? TunnelFacts.Server(username: " ")
            : TunnelFacts.Server(key: "");

        var error = (await Assert.ThrowsAsync<UserException>(() => pool.GetAsync(server).AsTask()))!;

        error.UserMessage.Should().Be(message);
        connector.Connects.Should().Be(0);
    }

    [Fact]
    public async Task FailedConnect_LeavesNothingBehind()
    {
        connector.OnConnect = endpoint => throw new SshConnectException(SshConnectFailure.Connect, endpoint);

        await Assert.ThrowsAsync<UserException>(() => pool.GetAsync(TunnelFacts.Server()).AsTask());

        connector.OnConnect = null;

        (await pool.GetAsync(TunnelFacts.Server())).Generation.Should().Be(1, "a failed connect is not a generation");
    }

    [Fact]
    public async Task OpenCommandChannel_RunsOneCommandAtATime_WaitingIsBoundedByTheCommandTimeout()
    {
        var server = TunnelFacts.Server();

        var session = await pool.GetAsync(server);

        using (var first = await session.OpenCommandChannelAsync(TunnelFacts.ConnectTimeout))
        {
            first.Server.Should().BeSameAs(server);

            // the first connection holds the session's command lock
            var timeout = (await Assert.ThrowsAsync<UserException>(
                () => session.OpenCommandChannelAsync(TimeSpan.FromMilliseconds(50))))!;

            timeout.UserMessage.Should().Be(SshErrors.CommandTimeoutMessage);
        }

        // released by Dispose: the next connection does not wait
        using var second = await session.OpenCommandChannelAsync(TimeSpan.FromMilliseconds(50));

        second.Server.Should().BeSameAs(server);
    }

    [Fact]
    public async Task OpenCommandChannel_DisposeCancelsTheRunningCommand()
    {
        var server = TunnelFacts.Server();

        var session = await pool.GetAsync(server);
        var commands = connector.LastChannel!.CommandChannel;

        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<SshCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        commands.OnExecute = _ =>
        {
            started.SetResult();
            return release.Task;
        };

        var connection = await session.OpenCommandChannelAsync(TunnelFacts.ConnectTimeout);

        var running = Task.Run(() => connection.Execute("long-running", out _));

        await started.Task;

        connection.Dispose();

        commands.CancelCalls.Should().Be(1, "disposing the connection cancels its running command");

        release.SetResult(new SshCommandResult(0, "done", string.Empty));

        (await running).Should().BeTrue();
        commands.CancelCalls.Should().Be(1, "a disposed connection is disposed only once");
    }

    [Fact]
    public async Task OpenCommandChannel_OnADroppedSession_IsAConnectError_AndStillReleasesTheLock()
    {
        var server = TunnelFacts.Server();

        var session = await pool.GetAsync(server);

        var connection = await session.OpenCommandChannelAsync(TunnelFacts.ConnectTimeout);

        pool.Fault(server.Id, session.Generation, "transport failure");

        var error = (await Assert.ThrowsAsync<UserException>(
            () => session.OpenCommandChannelAsync(TunnelFacts.ConnectTimeout)))!;

        error.UserMessage.Should().Be(SshErrors.ConnectMessage);

        connection.Dispose();

        var error_after = (await Assert.ThrowsAsync<UserException>(
            () => session.OpenCommandChannelAsync(TunnelFacts.ConnectTimeout)))!;

        error_after.UserMessage.Should().Be(SshErrors.ConnectMessage, "a dropped session never comes back by itself");
    }

    [Fact]
    public async Task ACommandThatLostTheSession_DropsIt_ForTheNextUse()
    {
        var server = TunnelFacts.Server();

        var session = await pool.GetAsync(server);
        var channel = connector.LastChannel!;
        channel.CommandChannel.OnExecute = _ => throw new SshConnectException(
            SshConnectFailure.Connect, connector.Endpoints[0], step: "command");

        using var connection = await session.OpenCommandChannelAsync(TunnelFacts.ConnectTimeout);

        var error = (await Assert.ThrowsAsync<UserException>(() => Task.Run(() => connection.Execute("print", out _))))!;

        error.UserMessage.Should().Be(SshErrors.ConnectMessage);
        channel.Disconnected.Should().BeTrue("a lost session is reported by the command path like by the forward paths");
        (await pool.GetAsync(server)).Generation.Should().Be(2);
    }

    [Fact]
    public async Task ACommandTimeout_KeepsTheSession()
    {
        var server = TunnelFacts.Server();

        var session = await pool.GetAsync(server);
        var channel = connector.LastChannel!;
        channel.CommandChannel.OnExecute = _ => throw new TimeoutException();

        using var connection = await session.OpenCommandChannelAsync(TunnelFacts.ConnectTimeout);

        await Assert.ThrowsAsync<UserException>(() => Task.Run(() => connection.Execute("sleep 1000", out _)));

        channel.Disconnected.Should().BeFalse("a slow command says nothing about the session");
        (await pool.GetAsync(server)).Should().BeSameAs(session);
    }

    [Fact]
    public async Task AForwardThatCannotStart_DropsTheSession_AndLeavesAsAConnectError()
    {
        var server = TunnelFacts.Server();

        var session = await pool.GetAsync(server);
        var channel = connector.LastChannel!;
        channel.ForwardFailure = new InvalidOperationException("channel open refused");

        var error = (await Assert.ThrowsAsync<UserException>(() => Task.Run(() => session.ForwardedPort(2053))))!;

        error.UserMessage.Should().Be(SshErrors.ConnectMessage);
        error.Message.Should().Contain("InvalidOperationException");
        channel.Disconnected.Should().BeTrue("a session that cannot forward is dropped");
        (await pool.GetAsync(server)).Generation.Should().Be(2);
    }

    [Fact]
    public async Task ForwardedPort_OutOfRange_IsRejectedWithoutTouchingTheSession()
    {
        var session = await pool.GetAsync(TunnelFacts.Server());

        var act = () => session.ForwardedPort(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
        connector.LastChannel!.Disconnected.Should().BeFalse();
    }

    [Fact]
    public async Task Sweep_NeverDropsASessionThatWasJustHandedOut()
    {
        var server = TunnelFacts.Server();

        var session = await pool.GetAsync(server);

        // idle long enough for the sweep, but a caller arrives first
        time.Advance(TimeSpan.FromSeconds(120));
        (await pool.GetAsync(server)).Should().BeSameAs(session);

        pool.Sweep().Should().Be(0, "the session was marked used under the entry lock");
        connector.LastChannel!.Disconnected.Should().BeFalse();
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        while (!condition()) await Task.Delay(10, limit.Token);
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");

        public void Advance(TimeSpan span) => now += span;

        public override DateTimeOffset GetUtcNow() => now;
    }
}
