using PhotonBypass.Domain.Profile;

namespace PhotonBypass.Application.Plan.Model;

public class PlanInfoModel
{
    public string Target { get; set; } = null!;

    public PlanType? Type { get; set; }

    public int? Value { get; set; }

    public int? SimultaneousUserCount { get; set; }
}
