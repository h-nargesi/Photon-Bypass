namespace PhotonBypass.Domain.Plan.Entity;

public interface IRenewalEntity
{
    public int SimultaneousUser { get; }

    public long? TrafficLimit { get; }

    public int? TimeLimitInDays { get; }
}