using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Domain.Plan;

public interface ISessionRadiusSyncService
{
    Task<List<UserConnectionBinding>> GetActiveConnections(int? realm_id, string username);
    
    Task<bool> CloseConnectionBySessionId(ServerEntity nas, string session_id);

    Task<bool> CloseConnectionByUsername(int? realm_id, string username);

    Task UpdateTrafficData(DateTime index);
}
