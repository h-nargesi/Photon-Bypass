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

    /// <summary>System identifier (≤ 15 chars) — admin views, alarms and logs (Docs/24 D5).</summary>
    public string Name { get; set; } = null!;

    /// <summary>Customer-facing text (≤ 50 chars); null or blank falls back to <see cref="Name"/>.</summary>
    public string? Title { get; set; }

    public DateTime? LastTrafficSync { get; set; }

    public long? CapacityOverrideBytes { get; set; }

    [NotMapped]
    public bool HasChanged {  get; set; }

    /// <summary>The only realm text customers see: trimmed <see cref="Title"/>, else <see cref="Name"/>.</summary>
    [NotMapped]
    public string DisplayTitle => string.IsNullOrWhiteSpace(Title) ? Name : Title.Trim();

    public DateTime Created { get; init; } = DateTime.Now;
}