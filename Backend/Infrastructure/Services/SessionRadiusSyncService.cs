using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Model;
using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Infra.Services;

public class SessionRadiusSyncService : ISessionRadiusSyncService
{
    public Task<List<UserConnectionBinding>> GetActiveConnections(NasEntity server, string username)
    {
        throw new NotImplementedException();
    }

    public Task<bool> CloseConnection(NasEntity server, string session_id)
    {
        throw new NotImplementedException();
    }

    public Task<bool> CloseConnections(IEnumerable<NasEntity> servers, string username, int count)
    {
        throw new NotImplementedException();
    }

    public Task UpdateTrafficData(IEnumerable<NasEntity> servers, DateTime index)
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