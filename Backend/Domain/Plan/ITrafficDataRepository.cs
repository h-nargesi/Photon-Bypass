using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Plan;

public interface ITrafficDataRepository : IEditableRepository<TrafficDataEntity>
{
    Task<List<TrafficDataEntity>> Fetch(string target, DateTime from);
    
    Task<Dictionary<int, List<TrafficDataEntity>>> Fetch(IEnumerable<int> nas_id, DateTime from);
}
