using PhotonBypass.Domain.Radius;

namespace PhotonBypass.Application.Management;

interface ServerManagementService
{
    Task<RealmEntity> GetCurrentRealm(int cloud_id);

    Task<int> GetAvalableServer();
}
