using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PhotonBypass.Tools;

namespace PhotonBypass.Domain.Servers.Model;

[Table("Nas")]
public class NasEntity : IBaseEntity, ISshServer
{
    [Key]
    public int Id { get; set; }

    public int RealmId { get; set; }

    public string Name { get; set; } = null!;
    
    public string DomainName { get; set; } = null!;

    public long BandWidth { get; set; }

    public string IpAddress { get; set; } = null!;

    public int SshPort { get; set; }

    public string SshUsername { get; set; } = null!;

    public string SshPassword { get; set; } = null!;

    public int Port => SshPort;
    
    public string Username => SshUsername;
    
    public string Password => SshPassword;
}
