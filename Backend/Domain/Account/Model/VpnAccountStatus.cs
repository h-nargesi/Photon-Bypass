namespace PhotonBypass.Domain.Account.Model;

/// <summary>
/// VPN-account status (Docs/22 D12): independent of the owner's portal
/// status. Suspended keeps the panel reachable but blocks renewal (D-A);
/// Deactivated is the terminal 40-day abandonment state.
/// </summary>
public enum VpnAccountStatus : byte
{
    Active = 0,

    Suspended = 1,

    Deactivated = 2,
}
