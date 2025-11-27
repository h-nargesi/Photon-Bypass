using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Infra.RadiusDesk;

public interface IAccountRadiusSyncService
{
    Task DeactivateUser(ServerEntity radius, IEnumerable<string> usernames);
}