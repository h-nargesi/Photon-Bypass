using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Account;

public interface IAccountRepository : IEditableRepository<AccountEntity>
{
    Task<AccountEntity?> GetAccount(int id);

    Task<AccountEntity?> GetAccount(string username);

    Task<AccountEntity?> GetAccountByMobile(string mobile);

    Task<AccountEntity?> GetAccountByEmail(string email);

    Task<IList<AccountEntity>> GetTargetArea(int account_id);

    Task<IDictionary<int, AccountEntity>> GetAccounts(IEnumerable<int> account_ids);

    Task<IDictionary<string, int>> GetAccountIdByUsername(IEnumerable<string> usernames);

    Task<int?> GetActiveAccountId(string username);

    Task<int> CheckUniqueData(string username, string? email, string? mobile);
}