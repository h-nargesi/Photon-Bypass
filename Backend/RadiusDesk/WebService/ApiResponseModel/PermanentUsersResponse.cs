using System.Text.Json.Serialization;

namespace PhotonBypass.RadiusDesk.WebService.ApiResponseModel;

class PermanentUsersResponse : RadiusServerResponseBase<PermanentUserEntityResponse>
{
    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }
}
