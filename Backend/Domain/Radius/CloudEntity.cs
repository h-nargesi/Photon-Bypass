using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PhotonBypass.Infra.Database;

namespace PhotonBypass.Domain.Radius;

[Table("clouds")]
public class CloudEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public string Name { get; set; } = null!;
}
