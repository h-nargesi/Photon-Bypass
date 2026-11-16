using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Account;

/// <summary>
/// VPN-account persistence (docs/22 D1/D2): per-realm VPN identities owned by
/// panel users. The username is globally unique and always starts with the
/// owner's panel username (D3).
/// </summary>
public interface IVpnAccountRepository : IEditableRepository<VpnAccountEntity>
{
    Task<List<VpnAccountEntity>> GetByUser(int user_id);

    /// <summary>Accounts not Deactivated (portal-visible set, docs/22 D12).</summary>
    Task<List<VpnAccountEntity>> GetVisibleByUser(int user_id);

    Task<List<VpnAccountEntity>> GetVisibleByUsers(IEnumerable<int> user_ids);

    Task<VpnAccountEntity?> GetAccount(int id);

    Task<VpnAccountEntity?> GetVisibleAccount(int id);

    Task<VpnAccountEntity?> GetAccount(string username);

    /// <summary>Every account id of the given usernames, any status (traffic merge, x-ui collector).</summary>
    Task<Dictionary<string, int>> GetAccountIdByUsername(IEnumerable<string> usernames);

    Task<Dictionary<int, string>> GetUsernamesByAccountId(IEnumerable<int> ids);

    /// <summary>Active accounts keyed by id — the radius-valid set (docs/22 D12).</summary>
    Task<Dictionary<int, VpnAccountEntity>> GetActiveAccounts(IEnumerable<int> account_ids);

    Task<List<VpnAccountEntity>> GetAll();

    /// <summary>
    /// The nested account rows of the management user list (docs/22 §3.6):
    /// per-account scalars plus the latest plan summary from TotalPlanState.
    /// </summary>
    Task<List<UserAccountOverviewModel>> GetAccountsOverview(IEnumerable<string> panel_usernames);

    /// <summary>First free candidate for the owner's next VPN username (D3 system suggestion).</summary>
    Task<string> SuggestUsername(string panel_username);

    Task UpdateWarningTimes(IEnumerable<int> account_ids);

    Task SetStatus(IEnumerable<int> account_ids, VpnAccountStatus status);
}
