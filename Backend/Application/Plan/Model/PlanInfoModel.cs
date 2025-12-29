namespace PhotonBypass.Application.Plan.Model;

public class PlanInfoModel
{
    public string Target { get; init; } = null!;

    public int? Days { get; init; }

    public double? Gigabytes { get; init; }

    public int? SimultaneousUserCount { get; init; }
}
