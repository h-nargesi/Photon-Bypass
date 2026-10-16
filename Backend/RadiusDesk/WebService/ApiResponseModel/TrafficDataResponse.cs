using System.Text.Json.Serialization;

namespace PhotonBypass.RadiusDesk.WebService.ApiResponseModel;

class TrafficDataResponse : RadiusServerResponseBase<TrafficDataEntityResponse>
{
    [JsonPropertyName("totalIn")]
    public long TotalIn { get; set; }

    [JsonPropertyName("totalOut")]
    public long TotalOut { get; set; }
}
