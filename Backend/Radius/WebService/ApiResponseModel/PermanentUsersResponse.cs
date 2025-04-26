using System.Text.Json.Serialization;

namespace PhotonBypass.Radius.WebService.ApiResponseModel;

class PermanentUsersResponse
{
    [JsonPropertyName("items")]
    public PermanentUserEntityResponse[]? Items { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }
}
