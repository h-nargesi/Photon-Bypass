namespace PhotonBypass.Application.Plan.Model;

public class UserPlanInfoModel
{
    public PlanType? Type { get; set; }

    public string RemainsTitle { get; set; } = null!;

    public int? RemainsPercent { get; set; }

    public int? SimultaneousUserCount { get; set; }
}
