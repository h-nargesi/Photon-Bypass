using PhotonBypass.Tools;
using System.Text.Json.Serialization;

namespace PhotonBypass.Radius.WebService.ApiResponseModel;

public class TrafficDataResponse
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("time_unit")]
    public int TimeUnit { get; set; }

    [JsonPropertyName("data_in")]
    [JsonConverter(typeof(StringNumberConverter))]
    public long DataIn { get; set; }

    [JsonPropertyName("data_out")]
    [JsonConverter(typeof(StringNumberConverter))]
    public long DataOut { get; set; }

    [JsonIgnore]
    public long TotalDat => DataIn + DataOut;
}
