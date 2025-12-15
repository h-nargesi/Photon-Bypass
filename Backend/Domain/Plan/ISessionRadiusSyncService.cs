using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Domain.Plan;

public interface ISessionRadiusSyncService
{
    Task<List<UserConnectionBinding>> GetActiveConnections(int? realm_id, string username);

    Task CloseConnectionBySessionId(ServerEntity nas, string session_id);

    Task CloseConnectionByUsername(int? realm_id, string username);

    Task<List<TrafficDataBinding>> GetTrafficData(DateTime index);
}
