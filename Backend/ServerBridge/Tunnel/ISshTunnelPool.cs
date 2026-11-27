using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.ServerBridge.Services;

namespace PhotonBypass.ServerBridge.Tunnel;

/// <summary>
/// One SSH session per server for the <c>SshTunnel</c> transport (Docs/25 D5).
/// Consumers only report transport failures; they never connect, disconnect
/// or rebuild a session themselves.
/// </summary>
public interface ISshTunnelPool
{
    /// <summary>
    /// Returns the live session of the server, connecting once when there is
    /// none. Sessions are keyed by <see cref="ServerEntity.Id"/>: a server with
    /// a non-positive id is rejected. Connect failures leave as a
    /// <see cref="ErrorHandler.UserException"/> with the wording of Docs/25 §4.2.
    /// </summary>
    ValueTask<ISshTunnelSession> GetAsync(ServerEntity server, CancellationToken ct = default);

    /// <summary>
    /// Drops the session a consumer used and saw failing — the one with the
    /// given <see cref="ISshTunnelSession.Generation"/>. A session that
    /// replaced it in the meantime stays alive: a late report of an old
    /// session never closes the next one.
    /// </summary>
    void Fault(int serverId, int generation, string reason);
}

public interface ISshTunnelSession
{
    /// <summary>Connect counter of the server: increments with every (re)connect.</summary>
    int Generation { get; }

    /// <summary>
    /// The loopback port that reaches the server's own <c>127.0.0.1:remotePort</c>;
    /// the forward is started once per session and port.
    /// </summary>
    int ForwardedPort(int remotePort);

    /// <summary>
    /// Opens a command connection. A session runs one command at a time:
    /// waiting longer than <paramref name="commandTimeout"/> throws a
    /// <see cref="ErrorHandler.UserException"/>. Disposing the connection
    /// cancels its running command and releases the session.
    /// </summary>
    Task<ISshConnection> OpenCommandChannelAsync(TimeSpan commandTimeout, CancellationToken ct = default);
}
