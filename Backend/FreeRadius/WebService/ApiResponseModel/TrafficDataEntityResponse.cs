using System.Text.Json.Serialization;

namespace PhotonBypass.Radius.WebService.ApiResponseModel;

class TrafficDataEntityResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("time_unit")]
    public int TimeUnit { get; set; }

    [JsonPropertyName("data_in")]
    public long DataIn { get; set; }

    [JsonPropertyName("data_out")]
    public long DataOut { get; set; }

    [JsonIgnore]
    public long TotalDat => DataIn + DataOut;
}
