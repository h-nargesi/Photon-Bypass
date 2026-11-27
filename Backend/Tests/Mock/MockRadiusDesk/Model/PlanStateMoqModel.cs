using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Plan.Entity;

namespace PhotonBypass.Test.Mock.MockRadiusDesk.Model;

public class PlanStateMoqModel
{
    public int Id { get; set; }

    public string Username { get; set; } = null!;

    public VpnAccountStatus Status { get; set; }

    public byte SimultaneousUser { get; set; }

    public int RealmId { get; set; }

    public int UserId { get; set; }

    public long? TrafficLimit { get; set; }

    public short? TimeLimitInDays { get; set; }

    public int? RateLimitInMeg { get; set; }

    public DateTime? LastConnectTime { get; set; }

    public DateTime? ExpirationDate { get; set; }

    public long? TrafficLeft { get; set; }


    public PlanStateEntity ToEntity()
    {
        return new PlanStateEntity
        {
            Id = Id,
            Username = Username,
            Status = Status,
            UserId = UserId,
            SimultaneousUser = SimultaneousUser,
            RealmId = RealmId,
            TrafficLimit = TrafficLimit,
            TimeLimitInDays = TimeLimitInDays,
            RateLimitInMeg = RateLimitInMeg,
            LastConnectTime = LastConnectTime,
            ExpirationDate = ExpirationDate,
            TrafficLeft = TrafficLeft
        };
    }
}