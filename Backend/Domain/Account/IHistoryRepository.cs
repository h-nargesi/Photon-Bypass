using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Plan;

public interface IHistoryRepository : IEditableRepository<HistoryEntity>
{
    Task<IList<HistoryEntity>> GetHistory(string target, DateTime? from, DateTime? to);
}
