using PhotonBypass.Domain;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PhotonBypass.FreeRadius.Entity;

[Table("realms")]
public class RealmEntity : IBaseEntity
{
    [Key]
    public int Id { get; set; }

    [Column("cloud_id")]
    public int CloudId { get; set; }

    public string Name { get; set; } = null!;

    public string Suffix { get; set; } = null!;

    [Column("city")]
    public string? Capacity { get; set; }

    [Column("country")]
    public string? RestrictedServerIP { get; set; }
}

[Table("ph_v_server_density")]
public class ServerDensityEntity : RealmEntity
{
    [Column("users_count")]
    public int UsersCount { get; set; }
}
