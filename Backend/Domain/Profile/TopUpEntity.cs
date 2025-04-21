using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Profile;

[Table("top_ups")]
public class TopUpEntity : IBaseEntity
{
    private const long BYTES_IN_GIG = 1024 * 1024 * 1024;

    [Key]
    public int Id { get; set; }

    [Column("cloud_id")]
    public int CloudId { get; set; }

    [Column("permanent_user_id")]
    public int PermanentUserId { get; set; }

    [Column("date")]
    public long? Data { get; set; }

    [NotMapped]
    public long? GigaData => Data != null ? (Data / BYTES_IN_GIG) : null;

    [Column("time")]
    public long? Time { get; set; }

    [Column("days_to_use")]
    public long? DaysToUse { get; set; }

    public string? Comment { get; set; }
}
