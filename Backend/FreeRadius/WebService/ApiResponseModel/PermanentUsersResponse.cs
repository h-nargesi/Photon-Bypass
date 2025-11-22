using System.Text.Json.Serialization;

namespace PhotonBypass.FreeRadius.WebService.ApiResponseModel;

class PermanentUsersResponse : RadiusServerResponseBase<PermanentUserEntityResponse>
{
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }
}
