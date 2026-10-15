using Microsoft.Extensions.DependencyInjection;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Infra.Radius;
using Serilog;

namespace PhotonBypass.Infra.Services;

public class SessionRadiusSyncService(
    IServiceProvider provider,
    Lazy<IServerRepository> server_repo)
    : ISessionRadiusSyncService
{
    private IServiceProvider Provider { get; } = provider;
    private Lazy<IServerRepository> ServerRepo { get; } = server_repo;

    private Lazy<IInfraSessionRadiusSyncService>? ResolveSync(ServerEntity radius)
    {
        var key = radius.GetSyncKey();

        if (key == null)
        {
            Log.Error("Unknown radius-server: (realm-id={0}, radius-id={1}, feature={2})",
                radius.RealmId, radius.Id, radius.Features);
            return null;
        }

        return Provider.GetRequiredKeyedService<Lazy<IInfraSessionRadiusSyncService>>(key);
    }

    public async Task<List<UserConnectionBinding>> GetActiveConnections(int? realm_id, string username)
    {
        var radius_list = await ServerRepo.Value.GetActiveRadiusInRealmOrAll(realm_id);

        if (radius_list.Count <= 0)
        {
            Log.Warning("No radius server found for realm-id: ({0})", realm_id);
            return [];
        }

        var result_list = await radius_list.RunJob(radius =>
        {
            var sync = ResolveSync(radius);
            return sync == null
                ? Task.FromResult(new List<UserConnectionBinding>())
                : sync.Value.GetActiveConnections(radius, username);
        });

        return result_list.SelectMany(list => list).ToList();
    }

    public async Task CloseConnectionBySessionId(ServerEntity nas, string session_id)
    {
        var radius_list = await ServerRepo.Value.GetActiveRadiusInRealmOrAll(nas.RealmId);

        if (radius_list.Count <= 0)
        {
            Log.Warning("No radius server found for realm-id: ({0})", nas.RealmId);
            return;
        }

        await radius_list.RunJob(radius =>
        {
            var sync = ResolveSync(radius);
            return sync == null ? Task.CompletedTask : sync.Value.CloseConnectionBySessionId(radius, nas, session_id);
        });
    }

    public async Task CloseConnectionByUsername(int? realm_id, string username)
    {
        var radius_list = await ServerRepo.Value.GetActiveRadiusInRealmOrAll(realm_id);

        if (radius_list.Count <= 0)
        {
            Log.Warning("No radius server found for realm-id: ({0})", realm_id);
            return;
        }

        await radius_list.RunJob(radius =>
        {
            var sync = ResolveSync(radius);
            return sync == null ? Task.CompletedTask : sync.Value.CloseConnectionByUsername(radius, username);
        });
    }

    public async Task<List<TrafficDataBinding>> GetTrafficData(Dictionary<int, DateTime?> indexes)
    {
        var radius_list = await ServerRepo.Value.GetAllActiveRadius();

        return await GetTrafficData(radius_list, indexes);
    }

    public async Task<List<TrafficDataBinding>> GetTrafficData(IReadOnlyCollection<ServerEntity> radius_list,
        Dictionary<int, DateTime?> indexes)
    {
        if (radius_list.Count <= 0) return [];

        var loaded_data_list_group = await radius_list.RunJob(radius_server =>
        {
            if (!indexes.TryGetValue(radius_server.RealmId, out var index))
            {
                index = null;
            }

            var sync = ResolveSync(radius_server);
            return sync == null
                ? Task.FromResult(new List<TrafficDataBinding>())
                : sync.Value.GetTrafficData(radius_server, index);
        });

        return [..loaded_data_list_group.SelectMany(list => list)];
    }
}
