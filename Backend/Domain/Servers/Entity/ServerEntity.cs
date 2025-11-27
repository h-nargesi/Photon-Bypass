using PhotonBypass.Domain.Servers.Types;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OperatingSystem = PhotonBypass.Domain.Servers.Types.OperatingSystem;

namespace PhotonBypass.Domain.Servers.Entity;

[Table("Server")]
public class ServerEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public bool Active { get; set; }

    public int RealmId { get; set; }

    public string IpAddress { get; set; } = null!;

    public string Name { get; set; } = null!;
    
    public string DomainName { get; set; } = null!;

    public long BandWidth { get; set; }

    public OperatingSystem OsType { get; set; } = 0;

    public ServerFeature Features { get; set; } = 0;

    public int SshPort { get; set; } = 22;

    public string SshUsername { get; set; } = null!;

    public string SshPassword { get; set; } = null!;

    public DateTime Created { get; set; } = DateTime.Now;
}
