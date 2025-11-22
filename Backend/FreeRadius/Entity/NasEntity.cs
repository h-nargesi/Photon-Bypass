using PhotonBypass.Domain;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.FreeRadius.Entity;

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
    public string SshPassword { get; set; } = null!;

    [Column("description")]
    public string? DomainName { get; set; }
}
