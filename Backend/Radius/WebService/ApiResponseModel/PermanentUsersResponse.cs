using System.Text.Json.Serialization;
using PhotonBypass.Domain.Profile;

namespace PhotonBypass.Radius.WebService.ApiResponseModel;

internal class PermanentUsersResponse
{
    [JsonPropertyName("items")]
    public PermanentUserEntity[]? Items { get; set; }

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }
}
