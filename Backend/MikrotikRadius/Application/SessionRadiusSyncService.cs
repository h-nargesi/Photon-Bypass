using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Infra.Radius.UserManager;
using PhotonBypass.Mikrotik.Radius.Model;
using PhotonBypass.ServerBridge;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.ServerBridge.Tik4net;
using tik4net.Objects;
using tik4net.Objects.Ppp;

namespace PhotonBypass.Mikrotik.Radius.Application;

public class SessionRadiusSyncService(ITik4NetHandler handler) : ISessionRadiusSyncService
{
    public async Task<List<UserConnectionBinding>> GetActiveConnections(ServerEntity radius, string username)
    {
        if (string.IsNullOrEmpty(username) || !InjectionRegex.Username().Match(username).Success)
        {
            throw new Exception("Username is not valid");
        }

        using var connection = await handler.ConnectTo(radius);

        var session_list = connection.LoadList<SessionModel>(
            TikParam.Equal<SessionModel>(nameof(SessionModel.Username), username),
            TikParam.Equal<SessionModel>(nameof(SessionModel.Active), "yes"));

        return [.. session_list.Select(session => new UserConnectionBinding
        {
            CallerId = session.CallerId,
            NasIpAddress = session.NasIpAddress,
            SessionId = session.SessionId,
            State = session.Active == "yes" ? ConnectionState.Up : ConnectionState.Down,
            UpTime = TimeSpan.Parse(session.UpTime ?? string.Empty),
            Username = session.Username,
        })];
    }

    public async Task CloseConnectionBySessionId(ServerEntity radius, ServerEntity nas, string session_id)
    {
        if (string.IsNullOrEmpty(session_id) || !InjectionRegex.SessionId().Match(session_id).Success)
        {
            throw new Exception("Session id is not valid");
        }

        using var connection = await handler.ConnectTo(radius);

        var session_list = connection.LoadList<PppActive>(
            TikParam.Equal<SessionModel>(nameof(SessionModel.SessionId), session_id))
            .ToList();

        foreach (var session in session_list)
        {
            connection.ExecuteNonQuery("close-session",
                TikParam.Equal<SessionModel>(nameof(SessionModel.Id), $"*{session.Id}"));
        }
    }

    public async Task CloseConnectionByUsername(ServerEntity radius, string username)
    {
        if (string.IsNullOrEmpty(username) || !InjectionRegex.Username().Match(username).Success)
        {
            throw new Exception("Username is not valid");
        }

        using var connection = await handler.ConnectTo(radius);

        var session_list = connection.LoadList<SessionModel>(
            TikParam.Equal<SessionModel>(nameof(SessionModel.Username), username));

        foreach (var session in session_list)
        {
            connection.ExecuteNonQuery("close-session",
                TikParam.Equal<SessionModel>(nameof(SessionModel.Id), $"*{session.Id}"));
        }
    }

    public async Task<List<TrafficDataBinding>> UpdateTrafficData(ServerEntity radius, DateTime index)
    {
        using var connection = await handler.ConnectTo(radius);

        return connection.LoadList<SessionModel>(
            TikParam.Greater<SessionModel>(nameof(SessionModel.Started), index.ToString("o")))
            .Select(session => new TrafficDataBinding
            {
                Id = session.Id,
                SessionId = session.SessionId ?? string.Empty,
                Username = session.Username ?? string.Empty,
                NasIpAddress = session.NasIpAddress,
                StartSession = DateTime.Parse(session.Started ?? string.Empty),
                EndSession = DateTime.Parse(session.Ended ?? string.Empty),
                DataIn = session.Download,
                DataOut = session.Upload,
            })
            .ToList();
    }
}