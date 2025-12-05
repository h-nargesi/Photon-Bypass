using PhotonBypass.Domain;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.FreeRadius.Entity;

[Table("top_ups")]
public class TopUpEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    [Column("cloud_id")]
    public int CloudId { get; set; }

    [Column("permanent_user_id")]
    public int PermanentUserId { get; set; }

    [Column("data")]
    public long? Data { get; set; }

    [NotMapped]
    public double? GigaData => Data / StaticValues.BytesInGigDouble;

    [Column("time")]
    public long? Time { get; set; }

    [Column("days_to_use")]
    public int? DaysToUse { get; set; }

    public string? Comment { get; set; }

    [Column("created")]
    public DateTime Created { get; init; }
}
