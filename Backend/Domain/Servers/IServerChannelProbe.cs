using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Model;

namespace PhotonBypass.Domain.Servers;

/// <summary>
/// Probes the SSH channel of a server for the admin connection test
/// (Docs/25 D8–D9): steps <c>tcp</c>, <c>host-key</c>, <c>auth</c> and — on
/// the tunnel transport — <c>forward</c>. Every call opens a throw-away
/// session through the connector, never the pooled one, so an unsaved
/// configuration can be tested and a discovery connect can stop before
/// authentication.
/// </summary>
public interface IServerChannelProbe
{
    Task<ChannelProbeResult> ProbeAsync(ServerEntity server, CancellationToken ct = default);
}

/// <summary>
/// The channel steps and what the service step needs: the presented host-key
/// fingerprint and the loopback port of the port-forward. The session stays
/// open until the result is disposed.
/// </summary>
public sealed class ChannelProbeResult : IAsyncDisposable
{
    private readonly Func<ValueTask> release;

    public ChannelProbeResult(
        List<ConnectionStepModel> steps, string? host_key_sha256 = null, int? forwarded_local_port = null,
        Func<ValueTask>? release = null)
    {
        Steps = steps;
        HostKeySha256 = host_key_sha256;
        ForwardedLocalPort = forwarded_local_port;
        this.release = release ?? (static () => ValueTask.CompletedTask);
    }

    public List<ConnectionStepModel> Steps { get; }

    public string? HostKeySha256 { get; }

    /// <summary>Null on the direct transport: the service is reached at its configured address.</summary>
    public int? ForwardedLocalPort { get; }

    public bool Passed => Steps.Count > 0 && Steps.All(step => step.Passed);

    public ValueTask DisposeAsync() => release();
}
