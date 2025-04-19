using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Account;

public interface IAccountRepository : IEditableRepository<AccountEntity>
{
    Task<AccountEntity?> GetAccount(string username);

    Task<AccountEntity?> GetAccountByMobile(string mobile);

    Task<AccountEntity?> GetAccountByEmail(string email);

    Task<IList<AccountEntity>> GetTargetArea(int userid);
}
