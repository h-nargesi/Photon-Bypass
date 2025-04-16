using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PhotonBypass.Infra.Database;

namespace PhotonBypass.Domain.Radius;

[Table("nas")]
public class NasEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    [Column("nasidentifier")]
    public string Name { get; set; } = null!;

    [Column("nasname")]
    public string IpAddress { get; set; } = null!;

    [Column("server")]
    public string ShhPassword { get; set; } = null!;
}
