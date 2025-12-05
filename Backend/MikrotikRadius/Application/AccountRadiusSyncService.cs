using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.OutSource.Model;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Infra.Nas;
using PhotonBypass.Infra.Radius.UserManager;
using PhotonBypass.Mikrotik.Radius.Model;
using PhotonBypass.ServerBridge;
using PhotonBypass.ServerBridge.Tik4net;
using tik4net.Objects;

namespace PhotonBypass.Mikrotik.Radius.Application;

public class AccountRadiusSyncService(Lazy<IMikrotikDirectService> mikrotik_direct_srv) : IAccountRadiusSyncService
{
    private Lazy<IMikrotikDirectService> MikrotikDirectSrv { get; } = mikrotik_direct_srv;

    public async Task RemoveUsers(ServerEntity radius, IEnumerable<string> usernames)
    {
        using var connection = await radius.TikApiConnect();

        foreach (var username in usernames)
        {
            if (string.IsNullOrEmpty(username) || !InjectionRegex.Username().Match(username).Success)
            {
                throw new Exception("Username is not valid");
            }

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

    public async Task DeactivateUserExcept(ServerEntity radius, HashSet<string> usernames)
    {
        using var connection = await radius.TikApiConnect();

        var all_users = connection.LoadAll<UserModel>()?
            .Select(u => u.Name ?? string.Empty)
            .Where(u => !usernames.Contains(u))
            .ToList();

        if (all_users?.Count > 0)
        {
            await DeactivateUser(radius, all_users);
        }
    }

    public async Task DeactivateUser(ServerEntity radius, IEnumerable<string> usernames)
    {
        var field_name = TikParam.GetFielName<UserModel>(nameof(UserModel.Disabled)) ??
            throw new Exception("The 'Disabled' TikProperty not found in 'UserModel'.");

        using var connection = await radius.TikApiConnect();

        foreach (var username in usernames)
        {
            if (string.IsNullOrEmpty(username) || !InjectionRegex.Username().Match(username).Success)
            {
                throw new Exception("Username is not valid");
            }

            if (!InjectionRegex.Username().Match(username).Success)
                continue;

            var user = connection.LoadList<UserModel>(
                TikParam.Equal<UserModel>(nameof(UserModel.Name), username))?
                .FirstOrDefault();

            if (user == null)
            {
                continue;
            }

            connection.Save(user, [field_name]);
        }
    }

    public async Task SyncUserAndActive(ServerEntity radius, AccountEntity account, RenewalEntity renewal)
    {
        using var connection = await radius.TikApiConnect();

        // check limitation (just by name)
        int? speed = null;
        var limitations = new HashSet<string>();
        if (renewal.TrafficLimit == null)
        {
            speed = 2;
            // Check Speed
            limitations.Add(connection.CheckRateLimit(speed.Value));
        }
        // Check Traffic
        if (renewal.TrafficLimit != null)
        {
            limitations.Add(connection.CheckTrafficLimit(renewal.TrafficLimit.Value));
        }
        
        // check profile (just by name)
        var profile_name = connection.CheckProfile(renewal.TimeLimitInDays, renewal.TrafficLimit, speed);
        
        // check profile-limitation assignment (just by name)
        connection.CheckLimitationAssignment(profile_name, limitations);
        
        // check user
        
        // active user
        // assign user to profile
        throw new NotImplementedException();
    }

    public async Task<string?> GetVpnPassword(ServerEntity radius, string username)
    {
        if (string.IsNullOrEmpty(username) || !InjectionRegex.Username().Match(username).Success)
        {
            throw new Exception("Username is not valid");
        }

        using var connection = await radius.TikApiConnect();

        var user = connection.LoadList<UserModel>(
            TikParam.Equal<UserModel>(nameof(UserModel.Name), username))?
            .FirstOrDefault();

        return user == null ? throw new Exception($"User not found: ({username}).") : user.Password;
    }

    public async Task ChangeVpnPassword(ServerEntity radius, string username, string password)
    {
        if (string.IsNullOrEmpty(username) || !InjectionRegex.Username().Match(username).Success)
        {
            throw new Exception("Username is not valid");
        }

        var field_name = TikParam.GetFielName<UserModel>(nameof(UserModel.Password)) ??
            throw new Exception("The 'Password' TikProperty not found in 'UserModel'.");

        using var connection = await radius.TikApiConnect();

        var user = connection.LoadList<UserModel>(
            TikParam.Equal<UserModel>(nameof(UserModel.Name), username))?
            .FirstOrDefault() ??
            throw new Exception($"User not found: ({username}).");

        user.Password = password;

        connection.Save(user, [field_name]);
    }
}
