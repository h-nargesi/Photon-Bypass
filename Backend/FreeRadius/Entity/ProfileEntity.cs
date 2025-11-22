using PhotonBypass.Domain;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.FreeRadius.Entity;

[Table("ph_v_all_profiles")]
public class ProfileEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    [Column("cloud_id")]
    public int CloudId { get; set; }

    public string Name { get; set; } = null!;

    [Column("cimultaneous_use")]
    public int? SimultaneousUse { get; set; }

    [Column("mikrotik_rate_limit")]
    public float? MikrotikRateLimit { get; set; }

    [Column("plan_type")]
    public PlanType PlanType { get; set; }
}
