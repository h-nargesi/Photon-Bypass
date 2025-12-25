using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Account;

public interface IWalletRepository : IEditableRepository<WalletEntity>
{
    Task<List<WalletEntity>> GetTransactions(int account_id);
    
    Task<int> GetBalance(int account_id);
}