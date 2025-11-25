using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.Domain.Account.Model;

namespace PhotonBypass.Domain.Account;

public interface ISessionRadiusSyncService
{
    Task<List<UserConnectionBinding>> GetActiveConnections(NasEntity server, string username);

    Task<bool> CloseConnection(NasEntity server, string session_id);

    Task<bool> CloseConnections(IEnumerable<NasEntity> servers, string username, int count);

    Task UpdateTrafficData(IEnumerable<NasEntity> servers);
}