using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Infra.Radius;
using Serilog;

namespace PhotonBypass.Xui.Application;

public class SessionRadiusSyncXuiService : IInfraSessionRadiusSyncService
{
    public Task<List<UserConnectionBinding>> GetActiveConnections(ServerEntity radius, string username)
    {
        return Task.FromResult(new List<UserConnectionBinding>());
    }

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
