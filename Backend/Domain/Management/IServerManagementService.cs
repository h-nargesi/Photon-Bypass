using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Services;

namespace PhotonBypass.Domain.Management;

public interface IServerManagementService
{
    Task<RealmEntity> GetAvalableRealm(int cloud_id);

    Task<CertContext> GetDefaultCertificate(int realmid);

    Task CheckUserServerBalance();
}
