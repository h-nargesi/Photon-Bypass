using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Plan;

public interface ITrafficDataRepository : IEditableRepository<TrafficDataEntity>
{
    Task<List<TrafficDataEntity>> Fetch(DateTime from);

    Task<List<TrafficDataEntity>> Fetch(int account_id, DateTime from);

    Task<List<TrafficDataEntity>> Fetch(int nas_id, IEnumerable<string> session_ids);

    Task<Dictionary<int, List<TrafficDataEntity>>> Fetch(IEnumerable<int> nas_ids, DateTime from);

    Task<List<TrafficDataEntity>> FetchOpen();

    Task<Dictionary<int, DateTime?>> LastUpdateTime();

    Task<Dictionary<int, int>> GetLastNasId(IEnumerable<int> account_ids, string session_prefix);
}
