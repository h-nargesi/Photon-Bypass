namespace PhotonBypass.Domain.Model.Plan;

public class PlanInfo
{
    public string Target { get; set; } = string.Empty;

    public PlanType Type { get; set; }

    public int Value { get; set; }

    public int SimultaneousUserCount { get; set; }
}
