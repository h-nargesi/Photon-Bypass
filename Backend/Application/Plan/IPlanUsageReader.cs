namespace PhotonBypass.Application.Plan;

public sealed record PlanUsageSnapshot
{
    public required int AccountId { get; init; }

    public required long TrafficUsed { get; init; }

    public long? TrafficLeft { get; init; }

    public DateTime? LastActiveTime { get; init; }

    public DateTime? ExpirationDate { get; init; }
}

public interface IPlanUsageReader
{
    Task<PlanUsageSnapshot?> GetUsage(int account_id);

    Task<Dictionary<int, PlanUsageSnapshot>> GetUsage(IEnumerable<int> account_ids);
}
