using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Account;

public interface ITrafficDataRepository : IEditableRepository<TrafficDataEntity>
{
    Task<List<TrafficDataEntity>> Fetch(string target, DateTime from);
    
    Task<Dictionary<int, List<TrafficDataEntity>>> Fetch(IEnumerable<int> nas_id, DateTime from);
}
