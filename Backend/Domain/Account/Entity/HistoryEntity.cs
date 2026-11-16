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

    /// <summary>Panel user the event belongs to (docs/22 D11).</summary>
    public int? Issuer { get; set; }

    public int Target { get; init; }

    /// <summary>Optional VPN account the event is about (docs/22 D11); null = user-level event.</summary>
    public int? VpnAccountId { get; init; }

    public EventCategory Category { get; init; }

    public EventType Type { get; init; }

    public string Title { get; init; } = null!;

    public string? Value { get; init; }

    public int? Price { get; init; }

    public string? Description { get; init; }

    public DateTime Created { get; init; } = DateTime.Now;
}
