using PhotonBypass.Domain.Session.Entity;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Session;

public interface IAccountRepository : IEditableRepository<AccountEntity>
{
    Task<AccountEntity?> GetAccount(int id);

    Task<AccountEntity?> GetAccount(string username);

    Task<AccountEntity?> GetAccountByMobile(string mobile);

    Task<AccountEntity?> GetAccountByEmail(string email);

    Task<IList<AccountEntity>> GetTargetArea(int account_id);

    Task<IDictionary<int, AccountEntity>> GetAccounts(IEnumerable<int> user_ids);

    Task<bool> CheckUsername(string username);

    Task<int?> GetRealmId(string username);
}