using PhotonBypass.Domain.Radius;
using PhotonBypass.Domain.Services;

namespace PhotonBypass.Domain.Server;

public interface IVpnNodeService
{
    Task<(NasEntity server, IList<UserConnectionBinding> connections)> GetActiveConnections(NasEntity server, string username);

    Task<bool> CloseConnection(string server, string username, string sessionId);

    Task<CertContext> GetCertificate(string server);
}
