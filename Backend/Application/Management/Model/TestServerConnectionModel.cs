using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Domain.Servers.Types;
using OperatingSystem = PhotonBypass.Domain.Servers.Types.OperatingSystem;

namespace PhotonBypass.Application.Management.Model;

/// <summary>
/// Body of the admin connection test (Docs/25 D9). Either an id alone — the
/// stored row is tested as it is — or the unsaved form, whose fields win over
/// the stored row. Nothing here is validated as a save payload: a form may be
/// tested before it is complete.
/// </summary>
public class TestServerConnectionModel
{
    public int Id { get; set; }

    public string? IpAddress { get; set; }

    public string? Name { get; set; }

    public OperatingSystem? OsType { get; set; }

    public ServerFeature? Features { get; set; }

    /// <summary>The config of the form; null keeps the stored one.</summary>
    public ServerConfiguration? Config { get; set; }
}
