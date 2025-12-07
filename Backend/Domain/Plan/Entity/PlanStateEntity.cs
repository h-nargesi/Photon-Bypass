using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Plan.Entity;

[Table("PlanState")]
public class PlanStateEntity : IRenewalEntity, IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public string Username { get; set; } = null!;
    
    public bool Active { get; set; }
    
    // Plan Info (Latest Renewal)
    public int SimultaneousUser { get; set; }

    public int? RestrictedRealmId { get; set; }

    public long? TrafficLimit { get; set; }
    
    public int? TimeLimitInDays { get; set; }

    // Last Usage Info
    public DateTime? LastConnectTime { get; set; }
    
    public bool LastConnectionWasSuccessful { get; set; }
    
    public string? LastConnectionMessage { get; set; }
    
    public long TrafficUsed { get; set; }

    // Remains
    public DateTime? ExpirationDate { get; set; }

    public long? TrafficLeft { get; set; }

    [NotMapped]
    public TimeSpan? TimeLeft => ExpirationDate - DateTime.Now;

    [NotMapped]
    public double? TrafficLeftPercent => 100 * TrafficLeft / (double?)TrafficLimit;

    [NotMapped]
    public double? TimeLeftPercent => TimeLeft.HasValue && TimeLimitInDays.HasValue ? 100 * TimeLeft.Value.TotalDays / (double)TimeLimitInDays : null;

    public DateTime Created { get; init; } = DateTime.Now;
}
