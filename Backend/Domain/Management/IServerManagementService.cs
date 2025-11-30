using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Domain.Management;

public interface IServerManagementService
{
    Task<RealmEntity> GetAvailableRealm();
    
    Task<CertContext> GetDefaultCertificate(int? realm_id);

    Task CheckUserServerBalance();
    
    Task UpdateTrafficData(DateTime index);
}
