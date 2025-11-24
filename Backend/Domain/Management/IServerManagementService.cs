using PhotonBypass.Domain.Servers.Model;
using PhotonBypass.Domain.Services.Model;

namespace PhotonBypass.Domain.Management;

public interface IServerManagementService
{
    Task<RealmEntity> GetAvailableRealm();

    Task<CertContext> GetDefaultCertificate(int realm_id);

    Task CheckUserServerBalance();
}
