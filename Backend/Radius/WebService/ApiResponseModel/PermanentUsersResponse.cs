using System.Text.Json.Serialization;

namespace PhotonBypass.Radius.WebService.ApiResponseModel;

class PermanentUsersResponse : RadiusServerResponseBase<PermanentUserEntityResponse>
{
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }
}
