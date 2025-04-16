using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PhotonBypass.Infra.Database;

namespace PhotonBypass.Domain.Radius;

[Table("permanent_users")]
public class PermenantUserEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    public string Username { get; set; } = null!;

    public string? Name { get; set; }

    public string? Surname { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public bool Active { get; set; }

    public string Realm { get; set; } = null!;

    [Column("realm_id")]
    public int RealmId { get; set; }

    public string Profile { get; set; } = null!;

    [Column("profile_id")]
    public int ProfileId { get; set; }

    [Column("from_date")]
    public DateTime? FromDate { get; set; }

    [Column("to_date")]
    public DateTime? ToDate { get; set; }

    [Column("cloud_id")]
    public int CloudId { get; set; }
}
