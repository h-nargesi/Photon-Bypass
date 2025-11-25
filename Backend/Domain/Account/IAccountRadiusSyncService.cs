using PhotonBypass.Domain.Session.Entity;
using PhotonBypass.Domain.Session.Entity;
using PhotonBypass.Domain.Servers.Types;

namespace PhotonBypass.Domain.Session;

// TODO: Move to Infrastructure
public interface IAccountRadiusSyncService
{
    Task<AccountEntity?> GetUser(string username);

    Task ActiveUser(int id, bool active);

    Task SaveUserPersonalInfo(AccountEntity account);

    Task RegisterUser(AccountEntity account);

    Task<bool> CheckUsername(string username);

    Task GetCertificate(NasEntity server, string username, CertContext default_context);
}
