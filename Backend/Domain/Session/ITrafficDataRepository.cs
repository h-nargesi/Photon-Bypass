using PhotonBypass.Domain.Session.Entity;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Session;

public interface ITrafficDataRepository : IEditableRepository<TrafficDataEntity>
{
    Task<List<TrafficDataEntity>> Fetch(string target, DateTime from);
    
    Task<Dictionary<int, List<TrafficDataEntity>>> Fetch(IEnumerable<int> nas_id, DateTime from);
}
