using System.Text.Json.Serialization;
using tik4net.Objects;

namespace PhotonBypass.Mikrotik.Radius.Model;

[TikEntity("/user-manager/user-profile")]
public class UserProfileModel
{
    [JsonPropertyName("id")]
    [TikProperty(".id", IsReadOnly = true, IsMandatory = true)]
    public string? Id { get; private set; }

    [JsonPropertyName("user")]
    [TikProperty("user")]
    public string? Username { get; set; }

    [JsonPropertyName("profile")]
    [TikProperty("profile")]
    public string? Profile { get; set; }

    [JsonPropertyName("end-time")]
    [TikProperty("end-time", IsReadOnly = true)]
    public string? EndTime { get; set; }

    [JsonPropertyName("state")]
    [TikProperty("state", IsReadOnly = true)]
    public string? State { get; set; }

    [JsonPropertyName("comment")]
    [TikProperty("comment")]
    public string? Comment { get; set; }
}