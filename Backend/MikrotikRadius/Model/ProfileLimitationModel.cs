using System.Text.Json.Serialization;
using tik4net.Objects;

namespace PhotonBypass.Mikrotik.Radius.Model;

[TikEntity("/user-manager/profile-limitation")]
public class ProfileLimitationModel
{
    [JsonPropertyName("id")]
    [TikProperty(".id", IsReadOnly = true, IsMandatory = true)]
    public string? Id { get; private set; }

    [JsonPropertyName("profile")]
    [TikProperty("profile", IsMandatory = true)]
    public string Profile { get; set; } = null!;

    [JsonPropertyName("limitation")]
    [TikProperty("limitation", IsMandatory = true)]
    public string Limitation { get; set; } = null!;
}