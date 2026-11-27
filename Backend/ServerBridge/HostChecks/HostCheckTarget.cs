using System.Globalization;

namespace PhotonBypass.ServerBridge.HostChecks;

/// <summary>
/// One outbound-TCP destination of the host-capability check: a literal
/// <c>host:port</c> pair. Shared by the options validation and the G1 probe.
/// </summary>
internal readonly record struct HostCheckTarget(string Host, int Port)
{
    public const string InvalidMessage =
        "ServerBridgeOptions:HostCheckTargets entries must be host:port with a port in 1..65535. See Docs/25-server-channel.md.";

    public static bool IsValid(string? value) => TryParse(value, out _);

    public static bool TryParse(string? value, out HostCheckTarget target)
    {
        target = default;

        if (string.IsNullOrWhiteSpace(value)) return false;

        var separator = value.IndexOf(':');

        if (separator < 1 || separator != value.LastIndexOf(':')) return false;

        var host = value[..separator];
        var port_text = value[(separator + 1)..];

        if (host.Any(char.IsWhiteSpace)) return false;

        if (!int.TryParse(port_text, NumberStyles.None, CultureInfo.InvariantCulture, out var port) ||
            port < 1 || port > 65535)
            return false;

        target = new HostCheckTarget(host, port);

        return true;
    }

    /// <summary>Deduplication key: the host is case-insensitive, the port is numeric.</summary>
    public string Key => $"{Host.ToLowerInvariant()}:{Port}";

    public override string ToString() => $"{Host}:{Port}";
}
