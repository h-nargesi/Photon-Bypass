using System.Text.RegularExpressions;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Infra.Radius.UserManager;
using PhotonBypass.Mikrotik.Radius.Model;
using PhotonBypass.ServerBridge.Tik4net;
using tik4net.Objects;

namespace PhotonBypass.Mikrotik.Radius;

public partial class AccountRadiusSyncService : IAccountRadiusSyncService
{
    public async Task RemoveUsers(ServerEntity radius, IEnumerable<string> usernames)
    {
        using var connection = await radius.TikApiConnect();

        foreach (var username in usernames)
        {
            if (!MyRegex().Match(username).Success)
                continue;
            
            var session_list = connection.LoadList<SessionModel>(
                TikParam.Equal<SessionModel>(nameof(SessionModel.Username), username));

            connection.Delete(session_list);

            var user_profiles = connection.LoadList<UserProfileModel>(
                TikParam.Equal<UserProfileModel>(nameof(UserProfileModel.Username), username));

            connection.Delete(user_profiles);

            var user = connection.LoadList<UserModel>(
                TikParam.Equal<UserModel>(nameof(UserModel.Name), username));

            connection.Delete(user);
        }
    }

    public Task DeactivateUserExcept(ServerEntity radius, IEnumerable<string> usernames)
    {
        throw new NotImplementedException();
    }

    public Task DeactivateUser(ServerEntity radius, IEnumerable<string> usernames)
    {
        throw new NotImplementedException();
    }

    public Task<bool> SyncUserAndActive(ServerEntity radius, AccountEntity account, RenewalEntity renewal)
    {
        throw new NotImplementedException();
    }

    public Task<object> GetCertificate(ServerEntity radius, string username, CertContext default_context)
    {
        throw new NotImplementedException();
    }

    public Task<bool> SetCertificate(ServerEntity radius, string username, object certificate)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetVpnPassword(ServerEntity radius, string username)
    {
        throw new NotImplementedException();
    }

    public Task<bool> ChangeVpnPassword(ServerEntity radius, string username, string password)
    {
        throw new NotImplementedException();
    }

    [GeneratedRegex(@"^[\d\w\-\.]+$")]
    private static partial Regex MyRegex();
}