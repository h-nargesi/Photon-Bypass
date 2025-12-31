using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Account;

public interface IAccountRepository : IEditableRepository<AccountEntity>
{
    Task<List<AccountEntity>> GetAllActive();

    Task<AccountEntity?> GetActiveAccount(int id);

    Task<AccountEntity?> GetAccount(string username);

    Task<AccountEntity?> GetAccountByMobile(string mobile);

    Task<AccountEntity?> GetAccountByEmail(string email);

    Task<List<AccountEntity>> GetActiveTargetArea(int account_id);

    Task<Dictionary<int, AccountEntity>> GetActiveAccounts(IEnumerable<int> account_ids);

    Task<Dictionary<string, int>> GetAccountIdByUsername(IEnumerable<string> usernames);

    Task<Dictionary<int, string>> GetUsernamesByAccountId(IEnumerable<int> ids);

    Task<int?> GetActiveAccountId(string username);

    Task<int> CheckUniqueData(string username, string? email, string? mobile);
}