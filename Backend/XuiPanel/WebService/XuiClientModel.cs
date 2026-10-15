using System.Text.Json.Serialization;

namespace PhotonBypass.Xui.WebService;

/// <summary>
/// x-ui client payload as carried inside the inbound `settings` JSON string.
/// Contract code-verified from alireza0/x-ui v1.12.0 source
/// (web/controller/inbound.go, database/model/model.go): `totalGB` is bytes
/// with 0 = unlimited, `expiryTime` is a unix-ms epoch with 0 = never
/// (negative = start on first use), `id` is the client UUID. Statistics are
/// keyed by email and survive UUID changes.
/// </summary>
public class XuiClientModel
{
    [JsonPropertyName("email")]
    public required string Email { get; set; }

    [JsonPropertyName("id")]
    public required string Uuid { get; set; }

    [JsonPropertyName("enable")]
    public bool Enable { get; set; } = true;

    [JsonPropertyName("totalGB")]
    public long TotalTrafficBytes { get; set; }

    [JsonPropertyName("expiryTime")]
    public long ExpiryTime { get; set; }

    [JsonPropertyName("limitIp")]
    public int LimitIp { get; set; }
}

public class XuiClientTrafficModel
{
    public required string Email { get; set; }

    public long Up { get; set; }

    public long Down { get; set; }

    public long Total => Up + Down;
}

/// <summary>
/// One entry of `POST xui/API/inbounds/onlines`: the client email plus its
/// online IPs mapped to the last-seen unix-ms timestamp.
/// </summary>
public class XuiOnlineClientModel
{
    [JsonPropertyName("email")]
    public string Email { get; set; } = null!;

    [JsonPropertyName("ips")]
    public Dictionary<string, long> Ips { get; set; } = [];
}
