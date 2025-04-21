using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Services;

namespace PhotonBypass.Domain.Management;

public interface IServerManagementService
{
    Task<RealmEntity> GetCurrentRealm(int cloud_id);

    Task<string> GetAvalableServer();

    Task<CertContext> GetDefaultCertificate(string realm);
}
