using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Profile.Model;

[Table("TopUp")]
public class TopUpEntity : IBaseEntity
{
    private const long BYTES_IN_GIG = 1024 * 1024 * 1024;

    [Key]
    public int Id { get; set; }

    public int CloudId { get; set; }

    public int AccountId { get; set; }

    public long? Data { get; set; }

    [NotMapped]
    public long? GigaData => Data / BYTES_IN_GIG;

    public long? Time { get; set; }

    public int? DaysToUse { get; set; }

    public string? Comment { get; set; }
}
