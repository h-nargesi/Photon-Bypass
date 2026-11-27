using System.Text.RegularExpressions;

namespace PhotonBypass.Domain.Servers.Types;

/// <summary>
/// Format rules of the <see cref="ServerTransport.SshTunnel"/> configuration
/// (Docs/25 §4.6), shared by the admin validation and the tunnel adapters: the
/// private key must be an OpenSSH one and the host-key pin an unpadded SHA256
/// fingerprint. Pins are normalized (trimmed, trailing <c>=</c> dropped) before
/// they are compared, so a pasted pin is accepted in either shape.
/// </summary>
public static partial class SshTunnelConfig
{
    public const string OpenSshKeyHeader = "-----BEGIN OPENSSH PRIVATE KEY-----";

    public static string NormalizeHostKeyPin(string? pin) => (pin ?? string.Empty).Trim().TrimEnd('=');

    public static bool IsValidHostKeyPin(string? pin) => HostKeyPinPattern().IsMatch(NormalizeHostKeyPin(pin));

    public static bool HasOpenSshKey(string? private_key) =>
        (private_key ?? string.Empty).TrimStart().StartsWith(OpenSshKeyHeader, StringComparison.Ordinal);

    [GeneratedRegex(@"^SHA256:[A-Za-z0-9+/]{43}$")]
    private static partial Regex HostKeyPinPattern();
}
