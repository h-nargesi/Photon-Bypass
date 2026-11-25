namespace PhotonBypass.Application.Basics.Model;

/// <summary>
/// Public, unauthenticated frontend configuration (Docs/20 D11) — carries
/// only what the UI needs (pre-login flags and public support texts); never
/// carries secrets.
/// </summary>
public class AppConfigModel
{
    public bool AllowRegistration { get; set; }

    /// <summary>Support contact shown on the dashboard card (Telegram handle for now; may become a phone number).</summary>
    public string? SupportContactInfo { get; set; }

    public string? SupportDescription { get; set; }
}
