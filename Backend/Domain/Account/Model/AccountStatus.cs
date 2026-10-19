namespace PhotonBypass.Domain.Account.Model;

/// <summary>
/// Two-stage account status (docs/20 D13): a suspended account keeps portal
/// access but loses its VPN config everywhere; a deactivated account loses
/// both. Renewal is blocked while the VPN is suspended (D-A).
/// </summary>
public enum AccountStatus : byte
{
    Active = 0,

    VpnSuspended = 1,

    Deactivated = 2,
}
