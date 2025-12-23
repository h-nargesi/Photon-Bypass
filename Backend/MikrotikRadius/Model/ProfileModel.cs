using System.Text.Json.Serialization;
using tik4net.Objects;

namespace PhotonBypass.Mikrotik.Radius.Model;

[TikEntity("/user-manager/profile")]
public class ProfileModel
{
    [JsonPropertyName("id")]
    [TikProperty(".id", IsReadOnly = true, IsMandatory = true)]
    public string? Id { get; private set; }

    [JsonPropertyName("name")]
    [TikProperty("name", IsMandatory = true)]
    public string Name { get; set; } = null!;

    [JsonPropertyName("validity")]
    [TikProperty("validity")]
    public string? Validity { get; set; }

    [JsonPropertyName("name-for-users")]
    [TikProperty("name-for-users")]
    public string? NameForUsers { get; set; }

    [JsonPropertyName("starts-when")]
    [TikProperty("starts-when")]
    public string? StartsWhen { get; set; }

    [JsonPropertyName("override-shared-users")]
    [TikProperty("override-shared-users")]
    public string? OverrideSharedUsers { get; set; }
}
