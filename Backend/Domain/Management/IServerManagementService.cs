using PhotonBypass.Domain.Profile;

namespace PhotonBypass.Domain.Management;

public interface IServerManagementService
{
    Task<RealmEntity> GetCurrentRealm(int cloud_id);

    Task<int> GetAvalableServer();
}
