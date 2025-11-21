using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Profile.Model;

[Table("Profile")]
public class ProfileEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public int CloudId { get; set; }

    public string Name { get; set; } = null!;

    public int? SimultaneousUse { get; set; }

    public string? MikrotikRateLimit { get; set; }
}
