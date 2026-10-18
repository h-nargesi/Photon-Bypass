using System.ComponentModel.DataAnnotations;

namespace PhotonBypass.Domain.OutSource.Model;

/// <summary>
/// Single source of every static brand value the user-facing artifacts share:
/// the connection-info text inside the VPN config download and the email
/// templates (logo, guide links, footer). Bound from the `BrandingOptions`
/// configuration section and validated on startup.
/// </summary>
public class BrandingOptions
{
    /// <summary>Brand name shown in the download header and email footers.</summary>
    [Required]
    public string Title { get; set; } = null!;

    [Required]
    [Url]
    public string SiteUrl { get; set; } = null!;

    /// <summary>Connection guide link used by the download text and emails.</summary>
    [Required]
    [Url]
    public string ConnectionGuideUrl { get; set; } = null!;

    /// <summary>
    /// Optional OpenVPN installer link; when empty the installer line is
    /// stripped from both the download text and the email template.
    /// </summary>
    [Url]
    public string? OvpnInstallerUrl { get; set; }

    /// <summary>Embedded (CID) email logo, relative to the working directory.</summary>
    [Required]
    public string LogoPath { get; set; } = "Branding/logo.png";
}
