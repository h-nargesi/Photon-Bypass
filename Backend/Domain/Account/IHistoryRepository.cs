namespace PhotonBypass.Domain.Account;

public interface IHistoryRepository
{
    Task<IList<HistoryEntity>> GetHistory(string target, DateTime? from, DateTime? to);
}
