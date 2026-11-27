namespace PhotonBypass.Domain.Servers.Model;

/// <summary>
/// One step of the admin connection test (Docs/25 D9), in the order it ran:
/// <see cref="Tcp"/>, <see cref="HostKey"/>, <see cref="Auth"/>, then
/// <see cref="Forward"/> (tunnel transport only) and <see cref="Service"/>.
/// The test stops at the first step that did not pass.
/// </summary>
public class ConnectionStepModel
{
    public const string Tcp = "tcp";

    public const string HostKey = "host-key";

    public const string Auth = "auth";

    public const string Forward = "forward";

    public const string Service = "service";

    public string Step { get; init; } = null!;

    public bool Passed { get; init; }

    /// <summary>Ports, durations, host-key fingerprints and general text only — never a key, password or cookie.</summary>
    public string? Detail { get; init; }

    public static ConnectionStepModel Pass(string step, string? detail = null) => new() { Step = step, Passed = true, Detail = detail };

    public static ConnectionStepModel Fail(string step, string? detail = null) => new() { Step = step, Passed = false, Detail = detail };
}
