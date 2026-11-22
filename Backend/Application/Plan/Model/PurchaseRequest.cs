namespace PhotonBypass.Application.Plan.Model;

/// <summary>
/// Purchase payload (Docs/24 §5): the chosen plan plus per-axis choices. Only
/// plan-defined axes may carry values — undefined axes stay null and the
/// calculator decides them.
/// </summary>
public class PurchaseRequest
{
    public int PlanId { get; set; }

    public byte? SimultaneousUserCount { get; set; }

    public short? Days { get; set; }

    public int? Gigabytes { get; set; }

    public int? RateLimitMeg { get; set; }

    /// <summary>The re-picked realm on a stale renewal (Docs/22 D6); mandatory on new accounts.</summary>
    public int? RealmId { get; set; }
}

/// <summary>First-purchase payload (Docs/22 D9) on top of the plan choice.</summary>
public class NewAccountRequest : PurchaseRequest
{
    /// <summary>User-supplied VPN username; null = accept the D3 system suggestion.</summary>
    public string? Username { get; set; }

    /// <summary>On-behalf owner username (Docs/20); null = the acting user themself.</summary>
    public string? Target { get; set; }
}

/// <summary>
/// The composed four-axis limitation of one purchase: validated buyer choices
/// on plan-defined axes, calculator values on undefined axes (Docs/24 D2).
/// Null = unlimited.
/// </summary>
public sealed record PurchaseLimits(byte? Users, short? Days, int? Gigabytes, int? RateLimitMeg);
