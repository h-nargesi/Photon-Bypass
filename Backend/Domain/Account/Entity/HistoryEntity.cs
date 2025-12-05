using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.Domain.Account.Entity;

[Table("History")]
public class HistoryEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public string Issuer { get; init; } = null!;

    public string Target { get; init; } = null!;

    public DateTime EventTime { get; init; }

    public string Title { get; init; } = null!;

    public string Color { get; set; } = null!;

    public object? Value { get; init; }

    public string? Unit { get; init; }

    public string? Description { get; init; }

    public DateTime Created { get; init; } = DateTime.Now;
}
