using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Profile.Model;

[Table("AccountProfile")]
public class AccountProfile
{
    [Key]
    public int Id { get; set; }

    public bool Active { get; set; }

    public int CloudId { get; set; }
    
    public int ProfileId { get; set; }

    public int? RestrictedRealmId { get; set; }
    
    public DateTime? LastConnectTime { get; set; }
    
    public bool LastConnectionWasSuccessful { get; set; }
    
    public string? LastConnectionMessage { get; set; }
    
    public long? TrafficLimit { get; set; }
    
    public DateTime? TimeLimit { get; set; }
}
