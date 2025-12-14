using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Account;

public interface IHistoryRepository : IEditableRepository<HistoryEntity>
{
    Task<List<HistoryEntity>> GetHistory(string target, DateTime? from, DateTime? to);

    Task Save(string issuer_name, HistoryEntity entity);
}
