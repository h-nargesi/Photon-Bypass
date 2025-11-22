using PhotonBypass.Domain;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.FreeRadius.Entity;

[Table("clouds")]
public class CloudEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public string Name { get; set; } = null!;
}
