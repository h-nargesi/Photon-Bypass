using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Servers.Model;

[Table("Realm")]
public class RealmEntity
{
    [Key]
    public int Id { get; set; }
    
    public int NasId { get; set; }

    public string DomainName { get; set; } = null!;
}