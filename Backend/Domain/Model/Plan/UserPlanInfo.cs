namespace PhotonBypass.Domain.Model.Plan;

public class UserPlanInfo
{
    public PlanType Type { get; set; }

    public string RemainsTitle { get; set; } = string.Empty;

    public int RemainsPercent { get; set; }

    public int SimultaneousUserCount { get; set; }
}
