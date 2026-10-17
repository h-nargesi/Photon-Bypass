using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.Tools;
using System.Text.Json.Serialization;
using OperatingSystem = PhotonBypass.Domain.Servers.Types.OperatingSystem;

namespace PhotonBypass.Application.Management.Model;

/// <summary>
/// API row of the admin server list (docs/20 phase 5, D8). Scalar fields plus
/// the <see cref="HasConfig"/> flag only — the stored configuration itself is
/// never exposed (write-without-read).
/// </summary>
public class ServerRowModel
{
    public int Id { get; set; }

    public bool IsActive { get; set; }

    public int RealmId { get; set; }

    public string? Realm { get; set; }

    public string IpAddress { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string DomainName { get; set; } = null!;

    public long BandWidth { get; set; }

    public OperatingSystem OsType { get; set; }

    public ServerFeature Features { get; set; }

    /// <summary>Whether a (protected) config is stored — the payload itself never leaves the server.</summary>
    public bool HasConfig { get; set; }

    [JsonConverter(typeof(UnixTimestampConverter))]
    public DateTime Created { get; set; }

    public string CreatedTitle { get; set; } = null!;
}
