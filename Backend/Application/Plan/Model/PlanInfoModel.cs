namespace PhotonBypass.Application.Plan.Model;

/// <summary>One selectable realm of the purchase flow (Docs/22 D4, open item 3).</summary>
public class RealmChoiceModel
{
    public int Id { get; init; }

    public string Name { get; init; } = null!;

    /// <summary>False when the realm has no free capacity or client slots (T4-E).</summary>
    public bool IsSelectable { get; init; }
}

public class PlanInfoModel
{
    public string Target { get; init; } = null!;

    public int AccountId { get; init; }

    public int RealmId { get; init; }

    /// <summary>True when the account is stale and renewal needs a realm re-pick (Docs/22 D6).</summary>
    public bool RequiresRealmChoice { get; init; }

    public int? Days { get; init; }

    public double? Gigabytes { get; init; }

    public int? SimultaneousUserCount { get; init; }

    public string? Kind { get; init; }

    public bool? SupportsOvpnCert { get; init; }

    public bool? SupportsPasswordChange { get; init; }

    public bool? SupportsCredentialRotation { get; init; }
}
