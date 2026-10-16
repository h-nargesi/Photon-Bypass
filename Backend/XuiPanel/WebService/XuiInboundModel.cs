using System.Text.Json.Serialization;

namespace PhotonBypass.Xui.WebService;

/// <summary>
/// The managed inbound's transport shape, code-verified against the
/// alireza0/x-ui v1.12.0/v1.14.0 inbound payload: `port` and `protocol` are
/// plain fields while `streamSettings` is itself a JSON string inside the
/// payload. The stream string is parsed by the vless link builder, never
/// copied into the local server configuration (docs/16 D4 live-read rule).
/// </summary>
public class XuiInboundModel
{
    public int Id { get; set; }

    public int Port { get; set; }

    public string Protocol { get; set; } = null!;

    public string? StreamSettings { get; set; }
}
