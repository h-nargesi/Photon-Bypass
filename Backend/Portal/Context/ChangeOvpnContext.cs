namespace PhotonBypass.Portal.Context;

public class ChangeOvpnContext
{
    /// <summary>VPN account the credential change applies to (docs/22 target contract).</summary>
    public int? AccountId { get; set; }

    public string? Target { get; set; }

    public string? Token { get; set; }

    public string? Password { get; set; }
}
