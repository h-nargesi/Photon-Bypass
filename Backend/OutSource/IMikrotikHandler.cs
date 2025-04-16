using PhotonBypass.Domain.Radius;
using PhotonBypass.OutSource.Mikrotik.Model;

namespace PhotonBypass.OutSource;

public interface IMikrotikHandler
{
    Task<(NasEntity server, IList<UserConnectionBinding> connections)> GetActiveConnections(NasEntity server, string username);

    Task<bool> CloseConnection(string server, string username, string sessionId);
}
