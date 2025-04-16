using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PhotonBypass.Infra.Database;

namespace PhotonBypass.Domain.Radius;

[Table("radact")]
public class RadAcctEntity : IBaseEntity
{
    [NotMapped]
    public int Id
    {
        get => RadAcctId;
        set => RadAcctId = value;
    }

    [Key]
    public int RadAcctId { get; set; }

    public string AcctSessionId { get; set; } = null!;

    public string Username { get; set; } = null!;

    public string NasIPAddress { get; set; } = null!;

    public string NasIdentifier { get; set; } = null!;

    public DateTime AcctStartTime { get; set; }
        
    public DateTime? AcctStopTime { get; set; }

    public string CallingStationId { get; set; } = null!;

    [NotMapped]
    public TimeSpan SessionUpTime => (AcctStopTime ?? DateTime.Now).Subtract(AcctStartTime);
}
