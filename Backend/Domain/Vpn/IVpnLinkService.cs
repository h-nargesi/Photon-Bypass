namespace PhotonBypass.Domain.Vpn;

/// <summary>
/// Builds the live client connection link for x-ui data-planes (docs/16 D4):
/// transport parameters are read live from the panel (listInbounds) and never
/// copied into the local server configuration. Implemented by the XuiPanel
/// adapter slice and consumed by the application layer through DI, like
/// <c>IEmailService</c>.
/// </summary>
public interface IVpnLinkService
{
    /// <summary>
    /// Returns the current vless link of the account on its realm's x-ui
    /// data-plane. Throws when the realm has no x-ui server or the client is
    /// missing on the panel; callers translate that into a friendly error.
    /// </summary>
    Task<string> GetVlessLink(int? realm_id, string username);
}
