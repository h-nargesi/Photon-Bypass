using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Model;

namespace PhotonBypass.Domain.Servers;

/// <summary>
/// Logs into one management service of a server with its real protocol — the
/// final judge of the admin connection test (Docs/25 D9): a forwarded channel
/// proves nothing until the service answers through it. One implementation
/// per Radius feature, keyed by <see cref="Types.ServerFeature.Xui"/> and
/// <see cref="Types.ServerFeature.UserManager"/>.
/// </summary>
public interface IServiceProbe
{
    /// <param name="forwarded_local_port">
    /// Loopback port of the connection test's port-forward on the tunnel
    /// transport; null on the direct transport.
    /// </param>
    Task<ConnectionStepModel> ProbeAsync(ServerEntity server, int? forwarded_local_port, CancellationToken ct = default);
}
