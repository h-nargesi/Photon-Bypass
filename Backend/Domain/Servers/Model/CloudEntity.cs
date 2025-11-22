using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Servers.Model;

[Table("Cloud")]
public class CloudEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public string Name { get; set; } = null!;
    
    public string Suffix { get; set; } = null!;
}
