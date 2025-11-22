using PhotonBypass.Domain;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.FreeRadius.Entity;

[Table("ph_v_users_balance")]
public class UserPlanStateEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    [Column("username")]
    public string Username { get; set; } = null!;

    [Column("simultaneous_user")]
    public int? SimultaneousUserCount { get; set; }

    [Column("account_disabled")]
    public string? AccountDisabled { get; set; }

    [Column("restricted_nas_ip")]
    public string? RestrictedServerIP { get; set; }

    [Column("plan_type")]
    public PlanType PlanType { get; set; }

    [Column("expiration")]
    public DateTime? ExpirationDate { get; set; }

    [Column("left_days")]
    public int? LeftDays { get; set; }

    [Column("left_hours")]
    public int? LeftHours { get; set; }

    [Column("giga_left")]
    public double? GigaLeft { get; set; }

    [Column("data_usage")]
    public double? DataUsage { get; set; }

    [Column("total_data_limit")]
    public double? TotalData { get; set; }
}
