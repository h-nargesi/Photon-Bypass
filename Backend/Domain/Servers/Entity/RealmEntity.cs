using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Servers.Entity;

[Table("Realm")]
public class RealmEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }
    
    public bool IsActive  { get; set; }

    public string Name { get; set; } = null!;

    public DateTime Created { get; set; } = DateTime.Now;
}