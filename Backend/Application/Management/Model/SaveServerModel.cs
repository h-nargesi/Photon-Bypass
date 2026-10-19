using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Domain.Servers.Types;
using OperatingSystem = PhotonBypass.Domain.Servers.Types.OperatingSystem;

namespace PhotonBypass.Application.Management.Model;

/// <summary>
/// Admin server create/update payload (docs/20 phase 5, D8). Id = 0 means
/// create. <see cref="Config"/> is sent as plain JSON and protected server-side
/// (<c>ServerConfigProtection.Protect</c>); on update a null config keeps the
/// stored protected blob untouched (write-without-read — it is never read back).
/// </summary>
public class SaveServerModel
{
    public int Id { get; set; }

    public int RealmId { get; set; }

    public string IpAddress { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string DomainName { get; set; } = null!;

    public long BandWidth { get; set; }

    /// <summary>Optional hard client-count cap (x-ui capacity gate, T4). Null = gate off.</summary>
    public int? MaxClients { get; set; }

    public OperatingSystem OsType { get; set; }

    public ServerFeature Features { get; set; }

    public ServerConfiguration? Config { get; set; }
}
