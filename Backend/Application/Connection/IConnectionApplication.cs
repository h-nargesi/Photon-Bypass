using PhotonBypass.Application.Connection.Model;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Connection;

public interface IConnectionApplication
{
    Task<ApiResult<List<ConnectionStateModel>>> GetCurrentConnectionState(int account_id);

    Task<ApiResult> CloseConnection(string ip, int account_id, string session_id);
}
