using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Types;

namespace PhotonBypass.Domain.Plan;

// TODO: Move to Infrastructure
public interface IAccountRadiusSyncService
{
    Task<AccountEntity?> GetUser(string username);

    Task ActiveUser(int id, bool active);

    Task<bool> SyncUserAndActive(AccountEntity account);

    Task<bool> CheckUsername(string username);

    Task GetCertificate(NasEntity server, string username, CertContext default_context);
}
