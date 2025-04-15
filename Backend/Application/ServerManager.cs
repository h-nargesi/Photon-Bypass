using PhotonBypass.Domain.Radius;

namespace PhotonBypass.Application;

interface IServerManagementService
{
    Task<RealmEntity> GetCurrentRealm(int cloud_id);

    Task<int> GetAvalableServer();
}
