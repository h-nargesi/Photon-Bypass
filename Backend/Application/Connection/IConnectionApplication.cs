using PhotonBypass.Application.Connection.Model;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.Application.Connection;

public interface IConnectionApplication
{
    Task<ApiResult<int[]>> GetCurrentConnectionState(string target);

    Task<ApiResult> CloseConnection(CloseConnectionContext context);
}
