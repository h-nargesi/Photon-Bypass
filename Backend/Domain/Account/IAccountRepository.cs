using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
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

    Task UpdateWarningTimes(IEnumerable<int> account_ids);

    Task SetDeactivated(IEnumerable<int> account_ids);

    Task<Dictionary<string, int>> GetAccountIdByUsername(IEnumerable<string> usernames);

    Task<Dictionary<int, string>> GetUsernamesByAccountId(IEnumerable<int> ids);

    Task<int?> GetActiveAccountId(string username);

    Task<int> CheckUniqueData(string username, string? email, string? mobile);

    /// <summary>
    /// Paged user list for the management panel (docs/20 phase 4): inactive
    /// accounts included; <paramref name="owner_id"/> scopes to one manager's
    /// subordinates (null = every account — the admin view);
    /// <paramref name="term"/> filters username/name/surname/email.
    /// </summary>
    Task<(List<AccountOverviewModel> Rows, int TotalCount)> SearchAccounts(int? owner_id, string? term, int page, int page_size);

    /// <summary>
    /// Whether the account still has at least one active subordinate — the
    /// guard that blocks demoting such a manager (docs/20 §3).
    /// </summary>
    Task<bool> HasActiveSubordinate(int manager_id);

    /// <summary>
    /// Attaches (or detaches with null) the Telegram chat of an account. The
    /// filtered unique index <c>UK_Account_TelegramChatId</c> rejects a chat
    /// already bound to another account — the raw SQL duplicate-key error
    /// (2601/2627) propagates so the bot reply can explain it.
    /// </summary>
    Task SetTelegramChat(int account_id, long? chat_id);
}