namespace PhotonBypass.Application.Plan.Model;

public class EstimateResult
{
    public int Price { get; init; }

    public int? Days { get; init; }

    public double? Gigabytes { get; init; }

    public int? SimultaneousUserCount { get; init; }

    public int? RateLimitMeg { get; init; }

    /// <summary>
    /// Docs/24 D5: stacking warnings — a purchased axis shadowed by the
    /// current plan's unlimited axis will not take effect.
    /// </summary>
    public IList<string> Warnings { get; init; } = [];
}
