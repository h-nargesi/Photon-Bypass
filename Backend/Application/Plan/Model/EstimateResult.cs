namespace PhotonBypass.Application.Plan.Model;

public class EstimateResult
{
    public int Price { get; init; }

    public int? Days { get; init; }

    public double? Gigabytes { get; init; }

    public int? SimultaneousUserCount { get; init; }
}
