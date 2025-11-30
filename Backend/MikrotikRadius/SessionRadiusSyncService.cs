using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Infra.Radius.UserManager;
using PhotonBypass.ServerBridge.Tik4net;
using System.Text.RegularExpressions;
using PhotonBypass.Mikrotik.Radius.ApiWrapper;
using tik4net.Objects;
using tik4net.Objects.Ppp;

namespace PhotonBypass.Mikrotik.Radius;

public partial class SessionRadiusSyncService(MikrotikApiCall call) : ISessionRadiusSyncService
{
    public async Task<List<UserConnectionBinding>> GetActiveConnections(ServerEntity radius, string username)
    {
        if (string.IsNullOrEmpty(username) || !UsernameCheck().Match(username).Success)
        {
            throw new Exception("Username is not valid");
        }

        var list = await call.PrepareApi<ISessions>(radius)
            .PrintByUsername(username);

        //return list.Select(session => new UserConnectionBinding
        //    //{
        //    //    CallerId = session.CallerId,
        //    //    NasIpAddress = session.NasIpAddress,
        //    //    SessionId = session.SessionId,
        //    //    State = ConnectionState.Up,
        //    //    UpTime = session.UpTime,
        //    //    Username = session.Username,
        //    //})
        //    .ToList();
        throw new NotImplementedException();
    }

    public async Task CloseConnectionBySessionId(ServerEntity radius, ServerEntity nas, string session_id)
    {
        if (string.IsNullOrEmpty(session_id) || !SessionIdCheck().Match(session_id).Success)
        {
            throw new Exception("Session id is not valid");
        }

        using var connection = await radius.TikApiConnect();

        var active_list = connection.LoadList<PppActive>(new TikFilterParam("session-id", session_id))
            .ToList();

        foreach (var active in active_list)
        {
            connection.Delete(active);
        }
    }

    public async Task CloseConnectionByUsername(ServerEntity radius, string username)
    {
        if (string.IsNullOrEmpty(username) || !UsernameCheck().Match(username).Success)
        {
            throw new Exception("Username is not valid");
        }

        using var connection = await radius.TikApiConnect();

        var active_list = connection.LoadList<PppActive>(new TikFilterParam("user", username))
            .ToList();

        foreach (var active in active_list)
        {
            connection.Delete(active);
        }
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