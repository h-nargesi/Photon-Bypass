namespace PhotonBypass.ServerBridge.Tunnel;

/// <summary>
/// What a connector does with the host key the server presents (Docs/25 D4).
/// </summary>
internal enum HostKeyPolicy
{
    /// <summary>Tunnel session: an empty or a mismatching pin disconnects before authentication.</summary>
    Required,

    /// <summary>Direct transport: a given pin must match; an empty pin is accepted.</summary>
    Optional,

    /// <summary>Connection test: record the fingerprint and disconnect before authentication.</summary>
    Discover,
}

internal enum HostKeyStatus
{
    /// <summary>The presented key matched the pin.</summary>
    Trusted,

    /// <summary>Accepted without a pin (direct transport).</summary>
    Unpinned,

    /// <summary>Recorded and refused before authentication (connection test).</summary>
    Discovered,
}

/// <summary>The host key a server presented during key exchange.</summary>
internal sealed record HostKeyCheck(string Sha256, string? KeyName, HostKeyStatus Status);
