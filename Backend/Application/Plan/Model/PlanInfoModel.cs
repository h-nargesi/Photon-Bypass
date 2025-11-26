namespace PhotonBypass.Application.Plan.Model;

public class PlanInfoModel
{
    public string Target { get; set; } = null!;

    public int? Days { get; set; }

    public double? Gigabytes { get; set; }

    public int? SimultaneousUserCount { get; set; }
}
