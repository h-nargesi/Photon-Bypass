using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Profile;

[Table("ph_v_users_balance")]
public class UserPlanStateEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    [Column("username")]
    public string Username { get; set; } = null!;

    [Column("simultaneous_user")]
    public int? SimultaneousUserCount { get; set; }

    [Column("reset_type_data")]
    public string? ResetTypeData { get; set; }

    [Column("left_days")]
    public int? LeftDays { get; set; }

    [Column("left_hours")]
    public int? LeftHours { get; set; }

    [Column("giga_left")]
    public double? GigaLeft { get; set; }

    [Column("total_data")]
    public double? TotalData { get; set; }

    [Column("data_usage")]
    public double? DataUsage { get; set; }
}
