namespace PhotonBypass.Application.Basics.Model;

/// <summary>
/// Public, unauthenticated frontend configuration (docs/20 D11) — mirrors
/// only the flags the pre-login UI needs; never carries secrets.
/// </summary>
public class AppConfigModel
{
    public bool AllowRegistration { get; set; }
}
