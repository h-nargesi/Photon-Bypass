namespace PhotonBypass.Domain.Management.Model;

public class RealmCapacityModel
{
    public int RealmId { get; init; }

    public string Name { get; init; } = null!;

    public long ComputedCapacityBytes { get; init; }

    public long? OverrideBytes { get; init; }

    public long EffectiveCapacityBytes { get; init; }

    public double DemandBytes { get; init; }

    public double ObservedUsageBytes { get; init; }

    public double Rate { get; init; }

    public bool IsEligibleForAssignment { get; init; }

    /// <summary>T4: distinct active accounts assigned to the realm (fan-out for unrestricted accounts).</summary>
    public int ActiveClients { get; init; }

    /// <summary>T4: min positive MaxClients over the realm's active x-ui servers; null = count gate off.</summary>
    public int? EffectiveMaxClients { get; init; }

    /// <summary>T4-E: band-eligible AND (no cap OR ActiveClients below the cap).</summary>
    public bool IsSelectableForAssignment { get; init; }
}
