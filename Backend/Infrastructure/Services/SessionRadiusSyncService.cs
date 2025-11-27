using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using Serilog;
using OperatingSystem = PhotonBypass.Domain.Servers.Types.OperatingSystem;

namespace PhotonBypass.Infra.Services;

public class SessionRadiusSyncService(
    Lazy<IServerRepository> ServerRepo,
    Lazy<ITrafficDataRepository> TrafficRepo,
    Lazy<MikrotikRadius.ISessionRadiusSyncService> MikrotikRadius,
    Lazy<RadiusDesk.ISessionRadiusSyncService> RadiusDesk)
    : ISessionRadiusSyncService
{
    public async Task<List<UserConnectionBinding>> GetActiveConnections(int? realm_id, string username)
    {
        var radius_list = await ServerRepo.Value.GetAllActiveRadiusInRealm(realm_id);

        if (radius_list.Count <= 0)
        {
            Log.Warning("No radius server found for realm-id: ({0})", realm_id);
            return [];
        }

        var result_list = await radius_list.RunJob(radius =>
        {
            switch (radius.Features)
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

    public async Task<bool> CloseConnectionBySessionId(ServerEntity nas, string session_id)
    {
        var radius_list = await ServerRepo.Value.GetAllActiveRadiusInRealm(nas.RealmId);

        if (radius_list.Count <= 0)
        {
            Log.Warning("No radius server found for realm-id: ({0})", nas.RealmId);
            return true;
        }

        var result_list = await radius_list.RunJob(radius =>
        {
            switch (radius.Features)
            {
                case ServerFeature.UserManager:
                    return MikrotikRadius.Value.CloseConnectionBySessionId(radius, nas, session_id);
                case ServerFeature.RadiusDesk:
                    return RadiusDesk.Value.CloseConnectionBySessionId(radius, nas, session_id);
                default:
                    Log.Error("Unknown radius-server: (realm-id={0}, radius-id={1}, feature={2})",
                        nas.RealmId, nas.Id, radius.Features);
                    return Task.FromResult(false);
            }
        });

        return result_list.Any(r => !r);
    }

    public async Task<bool> CloseConnectionByUsername(int? realm_id, string username)
    {
        var radius_list = await ServerRepo.Value.GetAllActiveRadiusInRealm(realm_id);

        if (radius_list.Count <= 0)
        {
            Log.Warning("No radius server found for realm-id: ({0})", realm_id);
            return true;
        }

        var result_list = await radius_list.RunJob(radius =>
        {
            switch (radius.Features)
            {
                case ServerFeature.UserManager:
                    return MikrotikRadius.Value.CloseConnectionByUsername(radius, username);
                case ServerFeature.RadiusDesk:
                    return RadiusDesk.Value.CloseConnectionByUsername(radius, username);
                default:
                    Log.Error("Unknown radius-server: (realm-id={0}, radius-id={1}, feature={2})",
                        realm_id, radius.Id, radius.Features);
                    return Task.FromResult(false);
            }
        });

        return result_list.Any(r => !r);
    }

    public async Task UpdateTrafficData(DateTime index)
    {
        var radius_list = await ServerRepo.Value.GetAllActiveRadiusInRealm((int?)null);

        if (radius_list.Count <= 0) return;

        var last_update_time = await TrafficRepo.Value.LastUpdateTime();

        if (last_update_time > index)
            index = last_update_time.Value;

        var current_traffic_data_task = TrafficRepo.Value.Fetch(index);
        
        var loaded_data_list_group = await radius_list.RunJob(radius_server =>
        {
            switch (radius_server.Features)
            {
                case ServerFeature.UserManager:
                    return MikrotikRadius.Value.UpdateTrafficData(radius_server, index);
                case ServerFeature.RadiusDesk:
                    return RadiusDesk.Value.UpdateTrafficData(radius_server, index);
                default:
                    Log.Error("Unknown radius-server: (realm-id={0}, radius-id={1}, feature={2})",
                        radius_server.RealmId, radius_server.Id, radius_server.Features);
                    return Task.FromResult(new List<TrafficDataEntity>());
            }
        });

        var traffic_data_list = Merge(
            await current_traffic_data_task,
            loaded_data_list_group.SelectMany(d => d),
            index);

        await TrafficRepo.Value.BachSave(traffic_data_list);
    }

    private static List<TrafficDataEntity> Merge(List<TrafficDataEntity> destination,
        IEnumerable<TrafficDataEntity> source, DateTime min_date_time)
    {
        var destination_dictionary = destination.ToDictionary(k => k.SessionId);
        var new_data = new List<TrafficDataEntity>();

        foreach (var traffic in source)
        {
            if (traffic.StartSession < min_date_time || traffic.StartSession >= DateTime.Now)
            {
                continue;
            }

            if (destination_dictionary.TryGetValue(traffic.SessionId, out var data))
            {
                if (data.DataIn == traffic.DataIn && data.DataOut == traffic.DataOut) continue;
                
                data.DataOut = traffic.DataOut;
                data.DataIn = traffic.DataIn;
                new_data.Add(data);
            }
            else
            {
                new_data.Add(traffic);
            }
        }

        return new_data;
    }
}