using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Domain.Plan;

public interface ISessionRadiusSyncService
{
    Task<List<UserConnectionBinding>> GetActiveConnections(int? realm_id, string username);

    Task<bool> DirectlyCloseConnection(ServerEntity server, string session_id);

    Task<bool> CloseConnections(int? realm_id, string username, int count);

    Task UpdateTrafficData(IEnumerable<ServerEntity> servers, DateTime index);
    
    Task UpdateTrafficData(string username, DateTime index);
}