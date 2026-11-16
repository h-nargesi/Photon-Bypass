using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Account;

public interface IHistoryRepository : IEditableRepository<HistoryEntity>
{
    Task<List<HistoryEntity>> GetHistory(int user_id, DateTime? from, DateTime? to);

    Task<Dictionary<int, HistoryEntity>> GetLastByTitle(string title);

    Task Save(string issuer_name, HistoryEntity entity);
}
