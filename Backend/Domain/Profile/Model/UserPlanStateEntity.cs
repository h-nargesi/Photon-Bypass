using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Profile;

[Table("UserPlanState")]
public class UserPlanStateEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public string Username { get; set; } = null!;

    public int? SimultaneousUserCount { get; set; }

    public bool AccountDisabled { get; set; }

    public string? RestrictedServerIP { get; set; }

    public DateTime? ExpirationDate { get; set; }

    public int? LeftDays { get; set; }

    public int? LeftHours { get; set; }

    public double? GigaLeft { get; set; }

    public double? DataUsage { get; set; }

    public double? TotalData { get; set; }
}
