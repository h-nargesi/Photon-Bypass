using PhotonBypass.Domain.Radius;

namespace PhotonBypass.Domain.Services;

public interface IVpnNodeService
{
    Task<(NasEntity server, IList<UserConnectionBinding> connections)> GetActiveConnections(NasEntity server, string username);

    Task<bool> CloseConnection(string server, string username, string sessionId);

    Task<bool> CloseConnections(string username, int count);

    Task<CertContext> GetCertificate(string server);
}
