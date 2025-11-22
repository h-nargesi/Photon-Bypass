using PhotonBypass.Application.Connection.Model;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Connection;

public interface IConnectionApplication
{
    Task<ApiResult<IList<ConnectionStateModel>>> GetCurrentConnectionState(string target);

    Task<ApiResult> CloseConnection(string server, string target, string session_id);
}
