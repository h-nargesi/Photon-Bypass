using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Servers.Types;

[Table("Realm")]
public class RealmEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public string Name { get; set; } = null!;
}