using PhotonBypass.Domain.Model.Connection;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.Domain;

public interface IConnectionApplication
{
    Task<ApiResult<int[]>> GetCurrentConnectionState(string target);

    Task<ApiResult> CloseConnection(CloseConnectionContext context);
}
