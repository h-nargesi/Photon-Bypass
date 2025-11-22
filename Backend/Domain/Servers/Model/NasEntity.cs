using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Servers.Model;

[Table("Nas")]
public class NasEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string IpAddress { get; set; } = null!;

    public string SshPassword { get; set; } = null!;

    public string DomainName { get; set; } = null!;
}
