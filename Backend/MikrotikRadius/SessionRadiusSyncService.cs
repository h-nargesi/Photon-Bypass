using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Infra.Radius.UserManager;
using PhotonBypass.Mikrotik.Base;
using PhotonBypass.Mikrotik.Radius.Scripts;

namespace PhotonBypass.Mikrotik.Radius;

public class SessionRadiusSyncService : ISessionRadiusSyncService
{
    public Task<List<UserConnectionBinding>> GetActiveConnections(ServerEntity radius, string username)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> CloseConnectionBySessionId(ServerEntity radius, ServerEntity nas, string session_id)
    {
        if (string.IsNullOrEmpty(session_id))
        {
            return false;
        }

        var context = new ProcessContext
        {
            ["session-id"] = session_id
        };

        var success = await BuiltInProcess.PppActiveRemoveBySession.ActivateOn(radius, context);
        return success && string.IsNullOrEmpty(context.Result);
    }

    public async Task<bool> CloseConnectionByUsername(ServerEntity radius, string username)
    {
        if (string.IsNullOrEmpty(username))
        {
            return false;
        }

        var context = new ProcessContext
        {
            ["username"] = username
        };

        var success = await BuiltInProcess.PppActiveRemoveBySession.ActivateOn(radius, context);
        return success && string.IsNullOrEmpty(context.Result);
    }

    public Task<List<TrafficDataEntity>> UpdateTrafficData(ServerEntity radius, DateTime index)
    {
        throw new NotImplementedException();
    }
}