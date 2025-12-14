using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Account;

public interface IAccountRepository : IEditableRepository<AccountEntity>
{
    Task<AccountEntity?> GetAccount(int id);

    Task<AccountEntity?> GetAccount(string username);

    Task<AccountEntity?> GetAccountByMobile(string mobile);

    Task<AccountEntity?> GetAccountByEmail(string email);

    Task<List<AccountEntity>> GetTargetArea(int account_id);

    Task<Dictionary<int, AccountEntity>> GetAccounts(IEnumerable<int> account_ids);

    Task<Dictionary<string, int>> GetAccountIdByUsername(IEnumerable<string> usernames);

    Task<Dictionary<int, string>> GetUsernamesByAccountId(IEnumerable<int> ids);

    Task<int?> GetActiveAccountId(string username);

    Task<int> CheckUniqueData(string username, string? email, string? mobile);
}