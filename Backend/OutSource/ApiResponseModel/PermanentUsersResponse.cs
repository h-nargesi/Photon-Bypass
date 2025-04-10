using PhotonBypass.OutSource.Model;

namespace OutSource.ApiResponseModel;

internal class PermanentUsersResponse
{
    public User[]? Items { get; set; }

    public bool Success { get; set; }

    public int TotalCount { get; set; }
}
