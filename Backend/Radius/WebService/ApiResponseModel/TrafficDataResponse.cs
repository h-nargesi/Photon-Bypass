using System.Text.Json.Serialization;

namespace PhotonBypass.Radius.WebService.ApiResponseModel;

class TrafficDataResponse
{
    [JsonPropertyName("items")]
    public TrafficDataEntityResponse[]? Items { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("totalIn")]
    public long TotalIn { get; set; }

    [JsonPropertyName("totalOut")]
    public long TotalOut { get; set; }
}
