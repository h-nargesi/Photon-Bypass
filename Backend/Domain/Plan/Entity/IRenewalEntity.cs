namespace PhotonBypass.Domain.Plan.Entity;

public interface IRenewalEntity
{
    public byte SimultaneousUser { get; }

    public long? TrafficLimit { get; }

    public short? TimeLimitInDays { get; }
}