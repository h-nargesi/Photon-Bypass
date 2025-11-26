using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Plan.Entity;

[Table("Renewal")]
public class RenewalEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public int AccountId { get; set; }

    public int? RestrictedRealmId { get; set; }

    public int? SimultaneousUse { get; set; }

    public long? TrafficLimit { get; set; }

    public int? TimeLimitInDays { get; set; }

    public string? Comment { get; set; }

    public DateTime Created { get; set; } = DateTime.Now;
}
