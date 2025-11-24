using PhotonBypass.Domain.Servers.Model;
using PhotonBypass.Domain.Services.Model;

namespace PhotonBypass.Domain.Services;

public interface IVpnNodeService
{
    Task<List<UserConnectionBinding>> GetActiveConnections(NasEntity server, string username);

    Task<bool> CloseConnection(NasEntity server, string session_id);

    Task<bool> CloseConnections(IEnumerable<NasEntity> servers, string username, int count);

    Task GetCertificate(NasEntity server, string username, CertContext default_context);
}
