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

    public DateTime? LastTrafficSync { get; set; }

    [NotMapped]
    public bool HasChanged {  get; set; }

    public DateTime Created { get; init; } = DateTime.Now;
}