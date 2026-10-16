namespace PhotonBypass.Application.Vpn.Model;

/// <summary>
/// Panel-display view of the account's connection config (docs/16 D4): the
/// data-plane kind plus, for x-ui accounts, the live vless link fetched from
/// the panel. Non-xui data-planes carry no extra payload (the OVPN cert is
/// downloaded/emailed instead).
/// </summary>
public class VpnConfigInfoModel
{
    public string? Kind { get; init; }

    public string? VlessLink { get; init; }
}

/// <summary>
/// A downloadable config file: for x-ui a plain-text file (username + vless
/// link), for Mikrotik the OVPN profile content unchanged.
/// </summary>
public class VpnConfigFileModel
{
    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public required byte[] Content { get; init; }
}
