using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Infra.FreeRadius;

public interface ISessionRadiusSyncService
{
    Task<List<UserConnectionBinding>> GetActiveConnections(ServerEntity server, string username);

    Task<bool> CloseConnection(ServerEntity server, string session_id);

    Task<bool> CloseConnections(IEnumerable<ServerEntity> servers, string username, int count);

    Task UpdateTrafficData(IEnumerable<ServerEntity> servers, DateTime index);
    
    Task UpdateTrafficData(string username, DateTime index);
}