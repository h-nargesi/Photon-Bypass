using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;
using OperatingSystem = PhotonBypass.Domain.Servers.Types.OperatingSystem;

namespace PhotonBypass.Infra.Services;

public class SessionRadiusSyncService(
    Lazy<MikrotikRadius.ISessionRadiusSyncService> MikrotikRadius,
    Lazy<MikrotikRadius.ISessionRadiusSyncService> FreeRadius) : ISessionRadiusSyncService
{
    public Task<List<UserConnectionBinding>> GetActiveConnections(int? realm_id, string username)
    {
        return server.Features switch
        {
            ServerFeature.MikrotikUserManager => MikrotikRadius.Value.GetActiveConnections(realm_id, username),
            ServerFeature.RadiusDesk => FreeRadius.Value.GetActiveConnections(realm_id, username),
            _ => throw new IndexOutOfRangeException("Unknown auth type")
        };
    }

    public Task<bool> DirectlyCloseConnection(ServerEntity server, string session_id)
    {
        return server.OsType switch
        {
            OperatingSystem.Mikrotik => MikrotikRadius.Value.CloseConnection(server, session_id),
            OperatingSystem.Ubuntu => throw new NotImplementedException("The ubuntu is not implemented!"),
            _ => throw new IndexOutOfRangeException("Unknown operating system type")
        };
    }

    public Task<bool> CloseConnections(int? realm_id, string username, int count)
    {
        var servers_dict = servers.GroupBy(s => s.OsType)
            .ToDictionary(k => k.Key, v => v.ToList());

        var m = servers_dict.Select(servers_type => servers_type.Key switch
        {
            OperatingSystem.Mikrotik => MikrotikRadius.Value.CloseConnections(servers_type.Value, username, count),
            OperatingSystem.Ubuntu => throw new NotImplementedException("The ubuntu is not implemented!"),
            _ => throw new IndexOutOfRangeException("Unknown operating system type")
        });

        return servers_dict.Aggregate(true, async (current, servers_type) => (bool)(current & servers_type.Key switch
        {
            OperatingSystem.Mikrotik => await MikrotikRadius.Value.CloseConnections(servers_type.Value, username, count),
            OperatingSystem.Ubuntu => throw new NotImplementedException("The ubuntu is not implemented!"),
            _ => throw new IndexOutOfRangeException("Unknown operating system type")
        }));
    }

    public Task UpdateTrafficData(IEnumerable<ServerEntity> servers, DateTime index)
    {
        throw new NotImplementedException();
    }

    public Task UpdateTrafficData(string username, DateTime index)
    {
        var new_data = Merge(ref list, data, min_date_time);

        if (new_data?.Count > 0)
        {
            int account_id;

            if (list.Count > 0) account_id = list[0].AccountId;
            else
            {
                var account = await AccountRepo.Value.GetAccount(target)
                              ?? throw new Exception($"Account not found: {target}");
                account_id = account.Id;
            }

            foreach (var record in new_data)
                record.AccountId = account_id;

            _ = TrafficDataRepo.Value.BachSave(new_data);
        }
    }

    private static DateTime? FindFirstEmptyDate(IEnumerable<TrafficDataEntity> data, DateTime from)
    {
        return data.OrderByDescending(x => x.Day)
            .Where(x => x.Day >= from)
            .Select(x => (DateTime?)x.Day)
            .FirstOrDefault()?
            .Date.AddDays(1);
    }

    private static List<TrafficDataEntity> Merge(ref List<TrafficDataEntity> destination,
        IEnumerable<TrafficDataRadius> source, DateTime minDateTime)
    {
        var destination_dict = destination.ToDictionary(k => k.Day);
        var new_data = new List<TrafficDataEntity>();

        foreach (var record in source)
        {
            if (record.Day < minDateTime || record.Day >= DateTime.Now)
            {
                continue;
            }

            if (destination_dict.TryGetValue(record.Day, out var data))
            {
                if (data.DataIn != record.DataIn || data.DataOut != record.DataOut)
                {
                    data.DataOut = record.DataOut;
                    data.DataIn = record.DataIn;
                    new_data.Add(data);
                }
            }
            else
            {
                var traffic = new TrafficDataEntity
                {
                    Day = record.Day,
                    DataIn = record.DataIn,
                    DataOut = record.DataOut,
                };

                new_data.Add(traffic);
                destination_dict.Add(record.Day, traffic);
            }
        }

        destination = [.. destination_dict.Values.OrderBy(x => x.Day)];

        return new_data;
    }
}