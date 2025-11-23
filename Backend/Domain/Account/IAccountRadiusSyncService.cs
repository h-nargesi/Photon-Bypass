using PhotonBypass.Domain.Account.Entity;

namespace PhotonBypass.Domain.Account;

public interface IAccountRadiusSyncService
{
    Task<AccountEntity?> GetUser(string username);

    Task ActiveUser(int id, bool active);

    Task SaveUserPersonalInfo(AccountEntity account);

    Task RegisterUser(AccountEntity account);

    Task<bool> CheckUsername(string username);
}
