using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Account;

public interface IWalletRepository : IEditableRepository<WalletEntity>
{
    Task<List<WalletEntity>> GetTransactions(int user_id);

    Task<List<WalletEntity>> GetInvoice(string target, int code);

    /// <summary>
    /// Newest-first wallet ledger page for one panel user (Docs/20 phase 3):
    /// server-side paging over the append-only rows.
    /// </summary>
    Task<(List<WalletEntity> Rows, int TotalCount)> GetHistory(int user_id, int page, int page_size);

    Task<Dictionary<int, int>> GetWalletsAmount(IEnumerable<int> ids);

    Task<List<int>> GetUserIdsBelowThreshold(int threshold);

    Task<int> GetBalance(int user_id);
}
