using System.Text.Json.Serialization;
using tik4net.Objects;

namespace PhotonBypass.Mikrotik.Radius.Model;

[TikEntity("/user-manager/limitation")]
public class LimitationModel
{
    [JsonPropertyName("id")]
    [TikProperty(".id", IsReadOnly = true, IsMandatory = true)]
    public string? Id { get; private set; }

    [JsonPropertyName("name")]
    [TikProperty("name", IsMandatory = true)]
    public string Name { get; set; } = null!;

    [JsonPropertyName("download-limit")]
    [TikProperty("download-limit")]
    public long? DownloadLimit { get; set; }

    [JsonPropertyName("upload-limit")]
    [TikProperty("upload-limit")]
    public long? UploadLimit { get; set; }

    [JsonPropertyName("transfer-limit")]
    [TikProperty("transfer-limit")]
    public long? TransferLimit { get; set; }

    [JsonPropertyName("rate-limit-rx")]
    [TikProperty("rate-limit-rx")]
    public string? RateLimitRx { get; set; }

    [JsonPropertyName("rate-limit-tx")]
    [TikProperty("rate-limit-tx")]
    public string? RateLimitTx { get; set; }

    [JsonPropertyName("rate-limit-burst-rx")]
    [TikProperty("rate-limit-burst-rx")]
    public string? RateLimitBurstRx { get; set; }

    [JsonPropertyName("rate-limit-burst-tx")]
    [TikProperty("rate-limit-burst-tx")]
    public string? RateLimitBurstTx { get; set; }
}