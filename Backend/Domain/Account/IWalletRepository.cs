using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Account;

public interface IWalletRepository : IEditableRepository<WalletEntity>
{
    Task<List<WalletEntity>> GetTransactions(int account_id);

    Task<List<WalletEntity>> GetNotPaid(int account_id);

    Task<List<WalletEntity>> GetInvoice(int code);

    Task<List<WalletEntity>> GetInvoice(string target, int code);

    Task<Dictionary<int, int>> GetWalletsAmount(IEnumerable<int> ids);

    Task<int> GenerateNewInvoiceCode();

    Task<int> GetBalance(int account_id);
}