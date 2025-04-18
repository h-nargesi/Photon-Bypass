using PhotonBypass.Domain.Radius;

namespace PhotonBypass.Domain.Server;

public interface IMikrotikHandler
{
    Task<(NasEntity server, IList<UserConnectionBinding> connections)> GetActiveConnections(NasEntity server, string username);

    Task<bool> CloseConnection(string server, string username, string sessionId);
}
