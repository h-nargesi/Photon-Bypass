using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Domain.Servers.Types;
using OperatingSystem = PhotonBypass.Domain.Servers.Types.OperatingSystem;

namespace PhotonBypass.Domain.Servers.Entity;

[Table("Server")]
public class ServerEntity : IBaseEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public bool IsActive { get; set; }

    public int RealmId { get; set; }

    public string IpAddress { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string DomainName { get; set; } = null!;

    public long BandWidth { get; set; }

    public OperatingSystem OsType { get; set; } = 0;

    public ServerFeature Features { get; set; } = 0;

    [Column("Config")]
    private string? JsonConfig { get; set; }

    [NotMapped]
    public ServerConfiguration? Config
    {
        get => JsonConfig != null ? JsonSerializer.Deserialize<ServerConfiguration>(JsonConfig) : null;
        set => JsonConfig = value != null ? JsonSerializer.Serialize(value) : null;
    }

    public DateTime Created { get; init; } = DateTime.Now;
}