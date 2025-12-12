using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Infra.Radius;

public interface ISessionRadiusSyncService
{
    Task<List<UserConnectionBinding>> GetActiveConnections(ServerEntity radius, string username);

    Task CloseConnectionBySessionId(ServerEntity radius, ServerEntity nas, string session_id);

    Task CloseConnectionByUsername(ServerEntity radius, string username);

    Task<List<TrafficDataBinding>> GetTrafficData(ServerEntity radius, DateTime index);
}