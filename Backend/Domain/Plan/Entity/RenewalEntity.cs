using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Plan.Entity;

[Table("Renewal")]
public class RenewalEntity : IRenewalEntity, IBaseEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int AccountId { get; set; }

    public int? RestrictedRealmId { get; set; }

    public byte SimultaneousUser { get; set; }

    public long? TrafficLimit { get; set; }

    public short? TimeLimitInDays { get; set; }

    public int? RateLimitInMeg { get; set; }

    public int WalletDebit { get; set; }

    public int? WalletCredit { get; set; }

    public string? Comment { get; set; }

    public long? LastSeenCounter { get; set; }

    public DateTime? LastPollTime { get; set; }

    public long TrafficUsed { get; set; }

    public DateTime? LastActiveTime { get; set; }

    // Stacked plan state (docs/17 D16): frozen once at purchase time by
    // PlanApplication.Renewal — the sole writer; NULL = no limit.
    public DateTime? ExpirationDate { get; set; }

    public long? CumulativeTrafficLeft { get; set; }

    public DateTime Created { get; init; } = DateTime.Now;
}
