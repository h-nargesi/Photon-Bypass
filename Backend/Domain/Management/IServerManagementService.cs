using PhotonBypass.Domain.Management.Model;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Result;

namespace PhotonBypass.Domain.Management;

public interface IServerManagementService
{
    Task<RealmEntity> GetAvailableRealm();

    Task<CertContext> GetDefaultCertificate(int? realm_id);

    Task CheckUserServerBalance();

    Task UpdateTrafficData();

    Task<DataPlaneInfoModel?> GetDataPlaneInfo(int? realm_id);

    Task<ApiResult> SetRealmCapacityOverride(int realm_id, long? override_bytes);

    Task<List<RealmCapacityModel>> GetRealmsCapacityState();
}
