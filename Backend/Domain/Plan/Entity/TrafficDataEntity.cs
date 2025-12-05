using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Plan.Entity;

[Table("TrafficData")]
public class TrafficDataEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public int AccountId { get; set; }

    public int NasId { get; set; }

    public string SessionId { get; set; } = null!;

    public DateTime StartSession { get; set; }

    public DateTime? EndSession { get; set; }

    public long DataIn { get; set; }

    public long DataOut { get; set; }

    public DateTime Created { get; init; } = DateTime.Now;

    [NotMapped]
    public long TotalData => DataIn + DataOut;

    [NotMapped]
    public TimeSpan Duration => (EndSession ?? DateTime.Now) - StartSession;
}
