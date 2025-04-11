namespace PhotonBypass.Application.Plan.Model;

public class RnewalContext
{
    public string? Target { get; set; }

    public PlanType? Type { get; set; }

    public int? Value { get; set; }

    public int? SimultaneousUserCount { get; set; }
}
