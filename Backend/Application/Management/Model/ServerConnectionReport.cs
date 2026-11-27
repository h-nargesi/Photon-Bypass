using PhotonBypass.Domain.Servers.Model;

namespace PhotonBypass.Application.Management.Model;

/// <summary>
/// Answer of the admin connection test (Docs/25 D9): every step in the order
/// it ran, the host-key fingerprint the server presented and the panel
/// machine the test ran on. It carries ports, durations, fingerprints and
/// general text only — never a key, password or cookie.
/// </summary>
public class ServerConnectionReport
{
    public bool Passed { get; init; }

    public List<ConnectionStepModel> Steps { get; init; } = [];

    /// <summary>The host key the server presented, for registration when the stored pin is empty.</summary>
    public string? HostKeySha256 { get; init; }

    public string MachineName { get; init; } = string.Empty;
}
