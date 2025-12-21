using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PhotonBypass.Domain.Account.Model;

namespace PhotonBypass.Domain.Account.Entity;

[Table("History")]
public class HistoryEntity : IBaseEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int? Issuer { get; set; }

    public int Target { get; init; }

    public EventCategory Category { get; init; }

    public EventType Type { get; init; }

    public string Title { get; init; } = null!;

    public string? Value { get; init; }

    public int? Price { get; init; }

    public string? Description { get; init; }

    public DateTime Created { get; init; } = DateTime.Now;
}
