using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.Infra.Radius.RadiusDesk;
using PhotonBypass.Infra.Radius.UserManager;
using Serilog;

namespace PhotonBypass.Infra.Services;

public class SessionRadiusSyncService(
    Lazy<IServerRepository> server_repo,
    Lazy<ISessionRadiusSyncUserManagerService> mikrotik_radius,
    Lazy<ISessionRadiusSyncRadiusDeskService> radius_desk)
    : ISessionRadiusSyncService
{
    private Lazy<IServerRepository> ServerRepo { get; } = server_repo;
    private Lazy<ISessionRadiusSyncUserManagerService> MikrotikRadius { get; } = mikrotik_radius;
    private Lazy<ISessionRadiusSyncRadiusDeskService> RadiusDesk { get; } = radius_desk;

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
            switch (radius.Features & ServerFeature.Radius)
            {
                case ServerFeature.UserManager:
                    return MikrotikRadius.Value.GetActiveConnections(radius, username);
                case ServerFeature.RadiusDesk:
                    return RadiusDesk.Value.GetActiveConnections(radius, username);
                default:
                    Log.Error("Unknown radius-server: (realm-id={0}, radius-id={1}, feature={2})",
                        realm_id, radius.Id, radius.Features);
                    return Task.FromResult(new List<UserConnectionBinding>());
            }
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
            switch (radius.Features & ServerFeature.Radius)
            {
                case ServerFeature.UserManager:
                    return MikrotikRadius.Value.CloseConnectionBySessionId(radius, nas, session_id);
                case ServerFeature.RadiusDesk:
                    return RadiusDesk.Value.CloseConnectionBySessionId(radius, nas, session_id);
                default:
                    Log.Error("Unknown radius-server: (realm-id={0}, radius-id={1}, feature={2})",
                        nas.RealmId, nas.Id, radius.Features);
                    return Task.CompletedTask;
            }
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
            switch (radius.Features & ServerFeature.Radius)
            {
                case ServerFeature.UserManager:
                    return MikrotikRadius.Value.CloseConnectionByUsername(radius, username);
                case ServerFeature.RadiusDesk:
                    return RadiusDesk.Value.CloseConnectionByUsername(radius, username);
                default:
                    Log.Error("Unknown radius-server: (realm-id={0}, radius-id={1}, feature={2})",
                        realm_id, radius.Id, radius.Features);
                    return Task.CompletedTask;
            }
        });
    }

    public async Task<List<TrafficDataBinding>> GetTrafficData(DateTime index)
    {
        var radius_list = await ServerRepo.Value.GetAllActiveRadius();

        if (radius_list.Count <= 0) return [];

        var loaded_data_list_group = await radius_list.RunJob(radius_server =>
        {
            switch (radius_server.Features & ServerFeature.Radius)
            {
                case ServerFeature.UserManager:
                    return MikrotikRadius.Value.GetTrafficData(radius_server, index);
                case ServerFeature.RadiusDesk:
                    return RadiusDesk.Value.GetTrafficData(radius_server, index);
                default:
                    Log.Error("Unknown radius-server: (realm-id={0}, radius-id={1}, feature={2})",
                        radius_server.RealmId, radius_server.Id, radius_server.Features);
                    return Task.FromResult(new List<TrafficDataBinding>());
            }
        });

        return [..loaded_data_list_group.SelectMany(list => list)];
    }
}