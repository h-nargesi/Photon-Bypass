using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Infra.Radius.UserManager;
using PhotonBypass.Mikrotik.Radius.Model;
using PhotonBypass.ServerBridge;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.ServerBridge.Tik4net;
using tik4net.Objects;

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
            CallerId = session.CallerId ?? string.Empty,
            NasIpAddress = session.NasIpAddress ?? string.Empty,
            SessionId = session.SessionId ?? string.Empty,
            State = session.Active == "yes" ? ConnectionState.Up : ConnectionState.Down,
            UpTime = TimeSpan.Parse(session.UpTime ?? string.Empty),
            Username = session.Username ?? string.Empty,
        })];
    }

    public async Task CloseConnectionBySessionId(ServerEntity radius, ServerEntity nas, string session_id)
    {
        if (string.IsNullOrEmpty(session_id) || !InjectionRegex.SessionId().Match(session_id).Success)
        {
            throw new Exception("Session id is not valid");
        }

        using var connection = await handler.ConnectTo(radius);

        var session_list = connection.LoadList<SessionModel>(
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

    public async Task<List<TrafficDataBinding>> GetTrafficData(ServerEntity radius, DateTime index)
    {
        using var connection = await handler.ConnectTo(radius);

        var data = connection.LoadList<SessionModel>(
            TikParam.Greater<SessionModel>(nameof(SessionModel.Started), index.AddSeconds(-1).ToString("s")))
            .ToList();

        return data
            .Select(session => new TrafficDataBinding
            {
                Id = session.Id,
                SessionId = session.SessionId ?? string.Empty,
                Username = session.Username ?? string.Empty,
                NasIpAddress = session.NasIpAddress,
                StartSession = DateTime.Parse(session.Started ?? string.Empty),
                EndSession = string.IsNullOrEmpty(session.Ended) ? null : DateTime.Parse(session.Ended),
                DataIn = session.Download,
                DataOut = session.Upload,
            })
            .ToList();
    }
}