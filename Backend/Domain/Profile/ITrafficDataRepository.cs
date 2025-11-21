using PhotonBypass.Domain.Profile.Model;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Profile;

public interface ITrafficDataRepository : IEditableRepository<TrafficDataEntity>
{
    Task<List<TrafficDataEntity>> Fetch(string target, DateTime from);
}
