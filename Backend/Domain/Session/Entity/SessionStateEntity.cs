using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Account.Entity;

[Table("SessionState")]
public class SessionStateEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public string Username { get; set; } = null!;
    
    // Plan Info (Latest Renewal)
    public int? SimultaneousUserCount { get; set; }

    public int? RestrictedRealmId { get; set; }

    public long? TrafficLimit { get; set; }
    
    public int? TimeLimit { get; set; }

    // Last Usage Info
    public DateTime? LastConnectTime { get; set; }
    
    public bool LastConnectionWasSuccessful { get; set; }
    
    public string? LastConnectionMessage { get; set; }
    
    public long TrafficUsed { get; set; }

    // Remains
    public DateTime? ExpirationDate { get; set; }

    public TimeSpan? TimeLeft { get; set; }

    public long? TrafficLeft { get; set; }
}
