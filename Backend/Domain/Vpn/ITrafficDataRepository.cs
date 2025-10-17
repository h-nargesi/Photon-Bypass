using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Vpn;

public interface ITrafficDataRepository : IEditableRepository<TrafficDataEntity>
{
    Task<List<TrafficDataEntity>> Fetch(string target, DateTime from);
}
