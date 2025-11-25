namespace PhotonBypass.Application.Plan.Model;

public class PlanInfoModel
{
    public string Target { get; set; } = null!;

    public int? Months { get; set; }

    public double? Gigabytes { get; set; }

    public int? SimultaneousUserCount { get; set; }
}
