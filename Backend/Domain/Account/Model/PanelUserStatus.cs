namespace PhotonBypass.Domain.Account.Model;

/// <summary>
/// Panel-user status (docs/22 D12): only the portal axis survives here —
/// VPN suspension is per <see cref="VpnAccountStatus"/>. Numeric values keep
/// the old two-stage semantics (0 active, 2 blocked).
/// </summary>
public enum PanelUserStatus : byte
{
    Active = 0,

    Deactivated = 2,
}
