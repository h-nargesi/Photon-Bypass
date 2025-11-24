using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Profile.Model;

[Table("TrafficData")]
public class TrafficDataEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public int AccountId { get; set; }

    public int NasId { get; set; }

    public DateTime StartSession { get; set; }

    public DateTime EndSession { get; set; }

    public long DataIn { get; set; }

    public long DataOut { get; set; }

    public long TotalData => DataIn + DataOut;
    
    public static TrafficDataEntity Empty { get; } = new TrafficDataEntity();
}
