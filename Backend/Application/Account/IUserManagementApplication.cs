using PhotonBypass.Application.Account.Model;
using PhotonBypass.Application.Model;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Account;

/// <summary>
/// Manager/admin user management (docs/20 phase 4, access matrix §3). All
/// actor→target relationships are validated live against the database — the
/// login-time target-area cache never gates these endpoints, so a freshly
/// created/attached subordinate is manageable within the same session.
/// </summary>
public interface IUserManagementApplication
{
    Task<ApiResult<PagedResult<UserRowModel>>> GetList(string? search, int page);

    Task<ApiResult> Create(CreateUserModel model);

    Task<ApiResult> SetPassword(string target, string password);

    Task<ApiResult> SetRole(string target, AccountRole role);

    /// <summary>Attach (<paramref name="owner"/> set) or detach (null) the target's owner.</summary>
    Task<ApiResult> SetOwner(string target, string? owner);

    Task<ApiResult> SetSelfService(string target, bool allow);

    /// <summary>
    /// Two-stage status change (docs/20 D13): Active = VPN on, VpnSuspended =
    /// VPN off but portal keeps working (renewal blocked, D-A), Deactivated =
    /// both off. Free transition between all three states; the immediate
    /// radius sync is best-effort — the hourly sweep reconciles.
    /// </summary>
    Task<ApiResult> SetStatus(string target, AccountStatus status);
}
