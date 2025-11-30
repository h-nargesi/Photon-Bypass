using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Infra.Dto;

namespace PhotonBypass.Infra.Radius;

public interface ISessionRadiusSyncService
{
    Task<List<UserConnectionBinding>> GetActiveConnections(ServerEntity radius, string username);

    Task CloseConnectionBySessionId(ServerEntity radius, ServerEntity nas, string session_id);

    Task CloseConnectionByUsername(ServerEntity radius, string username);

    Task<List<TrafficDataDto>> UpdateTrafficData(ServerEntity radius, DateTime index);
}