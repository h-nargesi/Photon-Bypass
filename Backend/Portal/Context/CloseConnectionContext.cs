namespace PhotonBypass.Portal.Context;

public class CloseConnectionContext
{
    /// <summary>VPN account the connection belongs to (Docs/22 target contract).</summary>
    public int? AccountId { get; set; }

    public string? Target { get; set; }

    public string? Server { get; set; }

    public string? SessionId { get; set; }
}
