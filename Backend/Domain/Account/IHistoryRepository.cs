using PhotonBypass.Domain.Session.Entity;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Session;

public interface IHistoryRepository : IEditableRepository<HistoryEntity>
{
    Task<IList<HistoryEntity>> GetHistory(string target, DateTime? from, DateTime? to);
}
