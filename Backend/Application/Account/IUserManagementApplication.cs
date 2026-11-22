using PhotonBypass.Application.Account.Model;
using PhotonBypass.Application.Model;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Account;

/// <summary>
/// Manager/admin user management (Docs/20 phase 4, access matrix §3). All
/// actor→target relationships are validated live against the database — the
/// login-time target-area cache never gates these endpoints, so a freshly
/// created/attached subordinate is manageable within the same session.
/// </summary>
public interface IUserManagementApplication
{
    Task<ApiResult<PagedResult<UserRowModel>>> GetList(string? search, int page);

    /// <summary>Creates a panel user only (Docs/22 D9): the VPN account arrives with the first purchase.</summary>
    Task<ApiResult> Create(CreateUserModel model);

    Task<ApiResult> SetPassword(string target, string password);

    Task<ApiResult> SetRole(string target, AccountRole role);

    /// <summary>Attach (<paramref name="owner"/> set) or detach (null) the target's owner.</summary>
    Task<ApiResult> SetOwner(string target, string? owner);

    Task<ApiResult> SetSelfService(string target, bool allow);

    /// <summary>Docs/24 D6: direct pricing-group move (admin only) — the snapshot-rule lever.</summary>
    Task<ApiResult> SetGroup(string target, int group_id);

    /// <summary>
    /// Docs/24 §5: flat VPN-account report over the plan-state universe —
    /// manager sees the subordinates' accounts, admin sees everyone;
    /// server-side paging, whitelisted sort (unlimited sorts last),
    /// username/owner search.
    /// </summary>
    Task<ApiResult<PagedResult<VpnAccountReportRowModel>>> GetAccountReport(string? search, int page, string? sort,
        bool descending);

    /// <summary>
    /// Panel-level status change (Docs/22 D12): Active = portal reachable,
    /// Deactivated = login blocked. The radius sync covers every VPN account
    /// of the user and is best-effort — the hourly sweep reconciles.
    /// </summary>
    Task<ApiResult> SetStatus(string target, PanelUserStatus status);

    /// <summary>
    /// VPN-level status change (Docs/22 D12): per-account suspension and
    /// activation/deactivation. Deactivated is the terminal abandonment
    /// state; the immediate radius sync is best-effort.
    /// </summary>
    Task<ApiResult> SetAccountStatus(int account_id, VpnAccountStatus status);
}
