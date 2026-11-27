using System.Text.Json.Serialization;
using PhotonBypass.Domain.Servers.Types;

namespace PhotonBypass.Domain.Servers.JsonType;

public class ServerConfiguration
{
    /// <summary>
    /// Stored and read as its name (<c>"Direct"</c> / <c>"SshTunnel"</c>); a row
    /// stored before the field existed reads as <see cref="ServerTransport.Direct"/>.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ServerTransport Transport { get; set; } = ServerTransport.Direct;

    public SshConfig? SshConfig { get; set; }

    public WebApiConfig? WebApiConfig { get; set; }

    public DataBaseInfo? DataBaseInfo { get; set; }
}
