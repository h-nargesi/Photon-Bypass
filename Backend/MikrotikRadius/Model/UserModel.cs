using System.Text.Json.Serialization;
using tik4net.Objects;

namespace PhotonBypass.Mikrotik.Radius.Model;

[TikEntity("/user-manager/user")]
public class UserModel
{
    [JsonPropertyName("id")]
    [TikProperty(".id", IsReadOnly = true)]
    public int Id { get; set; }

    [JsonPropertyName("disabled")]
    [TikProperty("disabled")]
    public bool Disabled { get; set; }

    [JsonPropertyName("name")]
    [TikProperty("name")]
    public string? Name { get; set; }

    [JsonPropertyName("password")]
    [TikProperty("password")]
    public string? Password { get; set; }

    [JsonPropertyName("shared-users")]
    [TikProperty("shared-users")]
    public int SharedUsers { get; set; } = 1;

    [JsonPropertyName("caller-id")]
    [TikProperty("caller-id")]
    public string? CallerId { get; set; }

    [JsonPropertyName("group")]
    [TikProperty("group")]
    public string? Group { get; set; }

    [JsonPropertyName("otp-secret")]
    [TikProperty("otp-secret")]
    public string? OptSecret { get; set; }

    [JsonPropertyName("comment")]
    [TikProperty("comment")]
    public string? Comment { get; set; }
}