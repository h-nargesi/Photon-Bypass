using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Services;

namespace PhotonBypass.Domain.Management;

public interface IServerManagementService
{
    Task<RealmEntity> GetAvailableNas(int cloud_id);

    Task<CertContext> GetDefaultCertificate(int realm_id);

    Task CheckUserServerBalance();
}
