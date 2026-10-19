using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Plan.Entity;

namespace PhotonBypass.Test.Mock.MockRadiusDesk.Model;

public class PlanStateMoqModel
{
    public int Id { get; set; }

    public string Username { get; set; } = null!;

    public AccountStatus Status { get; set; }

    public int RowNumber { get; set; }

    public byte SimultaneousUser { get; set; }

    public int? RestrictedRealmId { get; set; }

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
            RowNumber = RowNumber,
            SimultaneousUser = SimultaneousUser,
            RestrictedRealmId = RestrictedRealmId,
            TrafficLimit = TrafficLimit,
            TimeLimitInDays = TimeLimitInDays,
            RateLimitInMeg = RateLimitInMeg,
            LastConnectTime = LastConnectTime,
            ExpirationDate = ExpirationDate,
            TrafficLeft = TrafficLeft
        };
    }
}