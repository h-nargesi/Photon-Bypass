using PhotonBypass.Domain.Servers.Types;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Servers.Entity;

[Table("Nas")]
public class NasEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public bool Active { get; set; }

    public int RealmId { get; set; }

    public string Name { get; set; } = null!;
    
    public string DomainName { get; set; } = null!;

    public long BandWidth { get; set; }

    public string IpAddress { get; set; } = null!;

    public OsType OsType { get; set; }

    public int SshPort { get; set; }

    public string SshUsername { get; set; } = null!;

    public string SshPassword { get; set; } = null!;

    public DateTime Created { get; set; } = DateTime.Now;
}
