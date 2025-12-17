using System.Text.Json.Serialization;
using tik4net.Objects;

namespace PhotonBypass.Mikrotik.Radius.Model;

[TikEntity("/user-manager/session")]
public class SessionModel
{
    [JsonPropertyName("id")]
    [TikProperty(".id", IsReadOnly = true, IsMandatory = true)]
    public string? Id { get; private set; }

    [JsonPropertyName("active")]
    [TikProperty("active", IsReadOnly = true)]
    public string? Active { get; set; }

    [JsonPropertyName("user")]
    [TikProperty("user", IsReadOnly = true)]
    public string? Username { get; set; }

    [JsonPropertyName("acct-session-id")]
    [TikProperty("acct-session-id", IsReadOnly = true)]
    public string? SessionId { get; set; }

    [JsonPropertyName("nas-ip-address")]
    [TikProperty("nas-ip-address", IsReadOnly = true)]
    public string NasIpAddress { get; set; } = null!;

    [JsonPropertyName("calling-station-id")]
    [TikProperty("calling-station-id", IsReadOnly = true)]
    public string? CallerId { get; set; }

    [JsonPropertyName("started")]
    [TikProperty("started", IsReadOnly = true)]
    public string? Started { get; set; }

    [JsonPropertyName("ended")]
    [TikProperty("ended", IsReadOnly = true)]
    public string? Ended { get; set; }

    [JsonPropertyName("uptime")]
    [TikProperty("uptime", IsReadOnly = true)]
    public string? UpTime { get; set; }

    [JsonPropertyName("download")]
    [TikProperty("download", IsReadOnly = true)]
    public long Download { get; set; }

    [JsonPropertyName("upload")]
    [TikProperty("upload", IsReadOnly = true)]
    public long Upload { get; set; }
}