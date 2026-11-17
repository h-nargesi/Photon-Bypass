using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Account;

/// <summary>
/// Panel-user persistence (Docs/22 D1): everything the old
/// <c>IAccountRepository</c> did for portal identity, wallet anchoring and
/// manager subordination — minus the VPN fields that moved to
/// <see cref="IVpnAccountRepository"/>.
/// </summary>
public interface IPanelUserRepository : IEditableRepository<PanelUserEntity>
{
    Task<List<PanelUserEntity>> GetAllActive();

    Task<PanelUserEntity?> GetActiveUser(int id);

    Task<PanelUserEntity?> GetUser(string username);

    Task<PanelUserEntity?> GetUserByMobile(string mobile);

    Task<PanelUserEntity?> GetUserByEmail(string email);

    Task<List<PanelUserEntity>> GetActiveTargetArea(int user_id);

    Task<Dictionary<int, PanelUserEntity>> GetActiveUsers(IEnumerable<int> user_ids);

    Task SetDeactivated(IEnumerable<int> user_ids);

    Task<Dictionary<int, string>> GetUsernamesByUserId(IEnumerable<int> ids);

    Task<int?> GetActiveUserId(string username);

    Task<int> CheckUniqueData(string username, string? email, string? mobile);

    /// <summary>
    /// Paged user list for the management panel (Docs/20 phase 4): inactive
    /// users included; <paramref name="owner_id"/> scopes to one manager's
    /// subordinates (null = every user — the admin view);
    /// <paramref name="term"/> filters username/name/surname/email.
    /// </summary>
    Task<(List<UserOverviewModel> Rows, int TotalCount)> SearchUsers(int? owner_id, string? term, int page, int page_size);

    /// <summary>
    /// Whether the user still has at least one active subordinate — the
    /// guard that blocks demoting such a manager (Docs/20 §3).
    /// </summary>
    Task<bool> HasActiveSubordinate(int manager_id);

    /// <summary>
    /// Attaches (or detaches with null) the Telegram chat of a user. The
    /// filtered unique index <c>UK_PanelUser_TelegramChatId</c> rejects a chat
    /// already bound to another user — the raw SQL duplicate-key error
    /// (2601/2627) propagates so the bot reply can explain it.
    /// </summary>
    Task SetTelegramChat(int user_id, long? chat_id);
}
