using PhotonBypass.Domain.Profile;

namespace PhotonBypass.Radius.WebService.ApiResponseModel;

internal class PermanentUsersResponse
{
    public PermanentUserEntity[]? Items { get; set; }

    public bool Success { get; set; }

    public int TotalCount { get; set; }
}
