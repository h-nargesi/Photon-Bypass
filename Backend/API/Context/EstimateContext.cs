using PhotonBypass.Domain.Profile;

namespace PhotonBypass.API.Context;

public class EstimateContext
{
    public PlanType? Type { get; set; }

    public int? Value { get; set; }

    public int? SimultaneousUserCount { get; set; }
}
