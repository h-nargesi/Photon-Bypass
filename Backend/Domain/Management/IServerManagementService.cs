using PhotonBypass.Domain.Servers.Model;
using PhotonBypass.Domain.Services;

namespace PhotonBypass.Domain.Management;

public interface IServerManagementService
{
    Task<RealmEntity> GetAvailableNas();

    Task<CertContext> GetDefaultCertificate(int realm_id);

    Task CheckUserServerBalance();
}
