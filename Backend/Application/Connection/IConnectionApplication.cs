using PhotonBypass.Application.Connection.Model;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Connection;

public interface IConnectionApplication
{
    Task<ApiResult<List<ConnectionStateModel>>> GetCurrentConnectionState(string target);

    Task<ApiResult> CloseConnection(string ip, string target, string session_id);
}
