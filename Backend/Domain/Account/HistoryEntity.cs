using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Account;

[Table("History")]
public class HistoryEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public string Issuer { get; set; } = null!;

    public string Target { get; set; } = null!;

    public DateTime EventTime { get; set; }

    public string Title { get; set; } = null!;

    public string Color { get; set; } = null!;

    public object? Value { get; set; }

    public string? Unit { get; set; }

    public string? Description { get; set; }
}
