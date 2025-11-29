using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Infra.Radius.UserManager;
using PhotonBypass.ServerBridge.Tik4net;
using System.Text.RegularExpressions;
using tik4net.Objects;
using tik4net.Objects.Ppp;

namespace PhotonBypass.Mikrotik.Radius;

public partial class SessionRadiusSyncService : ISessionRadiusSyncService
{
    public Task<List<UserConnectionBinding>> GetActiveConnections(ServerEntity radius, string username)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> CloseConnectionBySessionId(ServerEntity radius, ServerEntity nas, string session_id)
    {
        if (string.IsNullOrEmpty(session_id) || !SessionIdCheck().Match(session_id).Success)
        {
            return false;
        }

        using var connection = await radius.TikApiConnect();

        var active_list = connection.LoadList<PppActive>(new TikFilterParam("session-id", session_id));

        foreach (var active in active_list)
        {
            connection.Delete(active);
        }

        return true;
    }

    public async Task<bool> CloseConnectionByUsername(ServerEntity radius, string username)
    {
        if (string.IsNullOrEmpty(username) || !UsernameCheck().Match(username).Success)
        {
            return false;
        }

        using var connection = await radius.TikApiConnect();

        var active_list = connection.LoadList<PppActive>(new TikFilterParam("user", username));

        foreach (var active in active_list)
        {
            connection.Delete(active);
        }

        return true;
    }

    public Task<List<TrafficDataEntity>> UpdateTrafficData(ServerEntity radius, DateTime index)
    {
        throw new NotImplementedException();
    }

    [GeneratedRegex("^[0-9a-f]+$", RegexOptions.Singleline)]
    private static partial Regex SessionIdCheck();

    [GeneratedRegex(@"^[\d\w\-\.]+$", RegexOptions.Singleline)]
    private static partial Regex UsernameCheck();
}