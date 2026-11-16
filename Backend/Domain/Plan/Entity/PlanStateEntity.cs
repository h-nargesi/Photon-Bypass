using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using PhotonBypass.Domain.Account.Model;

namespace PhotonBypass.Domain.Plan.Entity;

[Table("PlanState")]
public class PlanStateEntity : IRenewalEntity, IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public string Username { get; set; } = null!;

    /// <summary>Owner panel user (docs/22): the view joins VpnAccount.UserId in.</summary>
    public int UserId { get; set; }

    public VpnAccountStatus Status { get; set; }

    // Renewal ordinal within the account (ROW_NUMBER in TotalPlanState) — the
    // T4 client-count gate assigns an account to its highest passing row.
    public int RowNumber { get; set; }

    // Plan Info (Latest Renewal)
    public byte SimultaneousUser { get; set; }

    /// <summary>Realm of the VPN account (docs/22 §2: NOT NULL — bucket-0 is gone).</summary>
    public int RealmId { get; set; }

    public long? TrafficLimit { get; set; }

    public short? TimeLimitInDays { get; set; }

    public int? RateLimitInMeg { get; set; }

    // Last Usage Info
    public DateTime? LastConnectTime { get; set; }

    [NotMapped]
    public bool LastConnectionWasSuccessful { get; set; }

    [NotMapped]
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
