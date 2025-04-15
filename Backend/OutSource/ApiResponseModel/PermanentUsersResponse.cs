using PhotonBypass.Domain.Radius;

namespace OutSource.ApiResponseModel;

internal class PermanentUsersResponse
{
    public PermenantUserEntity[]? Items { get; set; }

    public bool Success { get; set; }

    public int TotalCount { get; set; }
}
