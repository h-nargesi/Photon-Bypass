using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Servers.Entity;

[Table("Realm")]
public class RealmEntity : IBaseEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public bool IsActive  { get; set; }

    public string Name { get; set; } = null!;

    /// <summary>User-facing display title (Persian); null falls back to <see cref="Name"/> (Docs/24 D8).</summary>
    public string? DisplayName { get; set; }

    public DateTime? LastTrafficSync { get; set; }

    public long? CapacityOverrideBytes { get; set; }

    [NotMapped]
    public bool HasChanged {  get; set; }

    public DateTime Created { get; init; } = DateTime.Now;
}