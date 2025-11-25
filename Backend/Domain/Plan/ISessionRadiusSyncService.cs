using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Domain.Plan;

public interface ISessionRadiusSyncService
{
    Task<List<UserConnectionBinding>> GetActiveConnections(NasEntity server, string username);

    Task<bool> CloseConnection(NasEntity server, string session_id);

    Task<bool> CloseConnections(IEnumerable<NasEntity> servers, string username, int count);

    Task UpdateTrafficData(IEnumerable<NasEntity> servers);
}