using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Infra.Nas;

public interface IMikrotikDirectService
{
    Task CloseConnection(ServerEntity server, string session_id);

    Task CloseConnections(IEnumerable<ServerEntity> servers, string username);

    Task<List<UserConnectionBinding>> GetActivePppConnections(ServerEntity server, string username);

    Task GetOVpnCertificate(ServerEntity server, string username, CertContext default_context);

    Task SetOVpnCertificate(ServerEntity server, string username, CertContext certificate);
}