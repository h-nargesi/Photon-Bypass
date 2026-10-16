using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Account;

public interface IWalletRepository : IEditableRepository<WalletEntity>
{
    Task<List<WalletEntity>> GetTransactions(int account_id);

    Task<List<WalletEntity>> GetInvoice(string target, int code);

    /// <summary>
    /// Newest-first wallet ledger page for one account (docs/20 phase 3):
    /// server-side paging over the append-only rows.
    /// </summary>
    Task<(List<WalletEntity> Rows, int TotalCount)> GetHistory(int account_id, int page, int page_size);

    Task<Dictionary<int, int>> GetWalletsAmount(IEnumerable<int> ids);

    Task<List<int>> GetAccountIdsBelowThreshold(int threshold);

    Task<int> GetBalance(int account_id);
}
