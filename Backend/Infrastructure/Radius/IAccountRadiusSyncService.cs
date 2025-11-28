using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Infra.Radius;

public interface IAccountRadiusSyncService
{
    Task RemoveUsers(ServerEntity radius, IEnumerable<string> usernames);
      
    Task DeactivateUserExcept(ServerEntity radius, IEnumerable<string> usernames);

    Task DeactivateUser(ServerEntity radius, IEnumerable<string> usernames);

    Task<bool> SyncUserAndActive(ServerEntity radius, AccountEntity account, RenewalEntity renewal);

    Task<object> GetCertificate(ServerEntity radius, string username, CertContext default_context);

    Task<bool> SetCertificate(ServerEntity radius, string username, object certificate);

    Task<string> GetVpnPassword(ServerEntity radius, string username);

    Task<bool> ChangeVpnPassword(ServerEntity radius, string username, string password);
}