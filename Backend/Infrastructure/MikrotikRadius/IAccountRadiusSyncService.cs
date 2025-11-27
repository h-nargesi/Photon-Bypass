using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Infra.MikrotikRadius;

public interface IAccountRadiusSyncService
{
    Task DeactivateUser(ServerEntity radius, IEnumerable<string> usernames);
}