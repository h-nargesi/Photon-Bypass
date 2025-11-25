using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Types;

namespace PhotonBypass.Domain.Management;

public interface IServerManagementService
{
    Task<RealmEntity> GetAvailableRealm();

    Task<CertContext> GetDefaultCertificate(int realm_id);

    Task CheckUserServerBalance();
}
