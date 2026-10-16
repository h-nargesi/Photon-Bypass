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
}
