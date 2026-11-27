using System.ComponentModel.DataAnnotations;

namespace PhotonBypass.ServerBridge;

public class ServerBridgeOptions
{
    public int SshConnectTimeoutSeconds { get; set; } = 15;

    public int SshKeepAliveIntervalSeconds { get; set; } = 30;

    public int SshCommandTimeoutSeconds { get; set; } = 30;

    public int Tik4NetOpenTimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Idle lifetime of a pooled SSH tunnel session (Docs/25 D5): a session
    /// with no activity for this long is disconnected by the sweep. The traffic
    /// collector and the hourly job keep a live server's session warm.
    /// </summary>
    [Range(60, 86400)]
    public int SshTunnelIdleSeconds { get; set; } = 900;

    /// <summary>
    /// Outbound-TCP probe targets of the host-capability check (Docs/25 D11),
    /// each <c>host:port</c>. They are probed at every start in addition to
    /// the SSH endpoint of every stored server row, so G1 answers before any
    /// server row exists.
    /// </summary>
    public List<string> HostCheckTargets { get; set; } = [];

    /// <summary>Interval of the worker-lifetime heartbeat line (Docs/25 G3).</summary>
    [Range(1, 1440)]
    public int HostCheckHeartbeatMinutes { get; set; } = 15;
}
