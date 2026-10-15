using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Infra.Radius;
using PhotonBypass.Xui.WebService;
using Serilog;

namespace PhotonBypass.Xui.Application;

public class SessionRadiusSyncXuiService(Lazy<IXuiApi> api) : IInfraSessionRadiusSyncService
{
    private Lazy<IXuiApi> Api { get; } = api;

    public async Task<List<UserConnectionBinding>> GetActiveConnections(ServerEntity radius, string username)
    {
        var online = await Api.Value.ListOnlineClients(radius);

        var client = online.FirstOrDefault(entry => entry.Email == username);

        if (client == null) return [];

        return client.Ips.Keys
            .Select(ip => new UserConnectionBinding
            {
                SessionId = $"xui:{ip}",
                NasIpAddress = radius.IpAddress,
                Username = username,
                CallerId = ip,
                UpTime = TimeSpan.Zero,
                State = ConnectionState.Up,
            })
            .ToList();
    }

    // A real disconnect is a client disable (updateClient with enable=false ⇒
    // RemoveUser upstream, plus blocking the online IPs on the server port);
    // closing a single x-ui "session" from here stays a no-op by design.
    public Task CloseConnectionBySessionId(ServerEntity radius, ServerEntity nas, string session_id)
    {
        Log.Warning("x-ui per-session close is not available yet: (realm-id={0}, nas-id={1}, session-id={2})",
            radius.RealmId, nas.Id, session_id);

        return Task.CompletedTask;
    }

    public Task CloseConnectionByUsername(ServerEntity radius, string username)
    {
        Log.Warning("x-ui per-session close is not available yet: (realm-id={0}, username={1})",
            radius.RealmId, username);

        return Task.CompletedTask;
    }

    public Task<List<TrafficDataBinding>> GetTrafficData(ServerEntity radius, DateTime? index)
    {
        return Task.FromResult(new List<TrafficDataBinding>());
    }
}
