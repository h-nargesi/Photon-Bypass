using System.Text.Json.Serialization;

namespace PhotonBypass.RadiusDesk.WebService.ApiResponseModel;

class RadiusServerResponseBase<T>
{
    [JsonPropertyName("items")]
    public T[]? Items { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }
}
