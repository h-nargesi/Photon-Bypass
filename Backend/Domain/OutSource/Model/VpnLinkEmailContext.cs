namespace PhotonBypass.Domain.OutSource.Model;

/// <summary>
/// Payload of the x-ui config delivery email (docs/16 D4): the live vless link
/// replaces the OpenVPN certificate attachment of the Mikrotik path.
/// </summary>
public class VpnLinkEmailContext
{
    public string Username { get; set; } = null!;

    public string Link { get; set; } = null!;
}
