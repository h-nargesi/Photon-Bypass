namespace PhotonBypass.Domain.Servers.Types;

/// <summary>
/// How the panel reaches a server's management services (Docs/25 D3).
/// <see cref="Direct"/> is the legacy path: SSH with a password and the x-ui /
/// User Manager API addressed at their configured host. <see cref="SshTunnel"/>
/// is one SSH session per server authenticated with a private key and a pinned
/// host key; the APIs ride it through a loopback port-forward.
/// </summary>
public enum ServerTransport
{
    Direct = 0,

    SshTunnel = 1,
}
