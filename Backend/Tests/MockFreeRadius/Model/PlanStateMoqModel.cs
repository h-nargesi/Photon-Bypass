using System.Text.Json.Serialization;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Test.MockOptions;

namespace PhotonBypass.Test.MockFreeRadius.Model;

public class PlanStateMoqModel
{
    public int Id { get; set; }

    public string Username { get; set; } = null!;
    
    public int SimultaneousUser { get; set; }

    public int? RestrictedRealmId { get; set; }

    public long? TrafficLimit { get; set; }
    
    public int? TimeLimitInDays { get; set; }

    public DateTime? LastConnectTime { get; set; }

    public DateTime? ExpirationDate { get; set; }

    public long? TrafficLeft { get; set; }
    

    public PlanStateEntity ToEntity()
    {
        return new PlanStateEntity
        {
            Id = Id,
            Username = Username,
            SimultaneousUser = SimultaneousUser,
            RestrictedRealmId = RestrictedRealmId,
            TrafficLimit = TrafficLimit,
            TimeLimitInDays = TimeLimitInDays,
            LastConnectTime = LastConnectTime,
            ExpirationDate = ExpirationDate,
            TrafficLeft = TrafficLeft
        };
    }
}