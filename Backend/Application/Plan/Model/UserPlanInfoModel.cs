namespace PhotonBypass.Application.Plan.Model;

public class UserPlanInfoModel
{
    public string RemainsTitle { get; set; } = null!;

    public int? RemainsTrafficPercent { get; set; }

    public int? RemainsTimePercent { get; set; }

    public int? SimultaneousUserCount { get; set; }
}
