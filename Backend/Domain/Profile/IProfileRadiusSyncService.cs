using PhotonBypass.Domain.Servers.Model;

namespace PhotonBypass.Domain.Profile;

public interface IProfileRadiusSyncService
{
    Task UpdateTrafficData(IEnumerable<NasEntity> servers);
}