using Microsoft.Extensions.Caching.Memory;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;

namespace PhotonBypass.Portal.Basical;

internal class AccessService(IMemoryCache cache, Lazy<IPanelUserRepository> panel_user_repo, Lazy<IVpnAccountRepository> vpn_account_repo) : IAccessService
{
    public async Task<bool> CheckAccess(string username, string target)
    {
        var area = await GetTargetArea(username);

        return area != null && (area.Role == AccountRole.Admin || area.Targets.Contains(target));
    }

    public async Task<bool> CheckAccountAccess(string username, int account_id)
    {
        var area = await GetTargetArea(username);

        if (area == null)
        {
            return false;
        }

        if (!area.AccountIdsTrusted)
        {
            // the login snapshot can miss an account bought seconds ago —
            // rebuild from the repositories before deciding (Docs/22 D9)
            area = await RebuildTargetArea(username) ?? area;

            if (area.Role != AccountRole.Admin)
            {
                cache.Set($"TargetArea|{username}", area, CacheEntryOptions());
            }
        }

        return area.Role == AccountRole.Admin || area.AccountIds.Contains(account_id);
    }

    public void LoginEvent(string username, AccountRole role, IEnumerable<string>? area, IEnumerable<int>? accountIds = null)
    {
        cache.Set($"TargetArea|{username}", new TargetAreaModel
        {
            Role = role,
            Targets = [.. (area ?? [])],
            AccountIds = [.. (accountIds ?? [])],
            AccountIdsTrusted = false,
        }, CacheEntryOptions());
    }

    public void LogoutEvent(string username)
    {
        InvalidateTargetArea(username);
    }

    public void InvalidateTargetArea(string username)
    {
        cache.Remove($"TargetArea|{username}");
    }

    /// <summary>
    /// A cache miss is not a denial: the login area is rebuilt from the
    /// repositories with the same rules the login path applies (Docs/14 M32,
    /// Docs/22 §3) — the account set is derived, not snapshotted, so a VPN
    /// account bought seconds ago is targetable without any invalidation.
    /// Admins cover every portal-accessible user and every account; everyone
    /// else exactly their own accounts plus their active subordinates'.
    /// Unknown or deactivated users are not rebuilt — access stays denied.
    /// </summary>
    private async Task<TargetAreaModel?> GetTargetArea(string username)
    {
        var area = cache.Get<TargetAreaModel>($"TargetArea|{username}");

        if (area != null)
        {
            return area;
        }

        area = await RebuildTargetArea(username);

        if (area == null)
        {
            return null;
        }

        cache.Set($"TargetArea|{username}", area, CacheEntryOptions());

        return area;
    }

    private async Task<TargetAreaModel?> RebuildTargetArea(string username)
    {
        var account = await panel_user_repo.Value.GetUser(username);

        if (account is not { Status: not PanelUserStatus.Deactivated })
        {
            return null;
        }

        List<PanelUserEntity> area_users;

        if (account.Role == AccountRole.Admin)
        {
            area_users = await panel_user_repo.Value.GetAllActive();
        }
        else
        {
            area_users = [account, .. await panel_user_repo.Value.GetActiveTargetArea(account.Id)];
        }

        // own + subordinate accounts; suspended accounts stay operable
        // (portal-visible set), deactivated ones drop out
        var area_accounts = await vpn_account_repo.Value.GetVisibleByUsers(area_users.Select(user => user.Id));

        return new TargetAreaModel
        {
            Role = account.Role,
            Targets = [.. area_users.Where(user => user.Id != account.Id).Select(active => active.Username)],
            AccountIds = [.. area_accounts.Select(vpn_account => vpn_account.Id)],
            AccountIdsTrusted = true,
        };
    }

    private static MemoryCacheEntryOptions CacheEntryOptions()
    {
        return new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2),
            Size = 1,
        };
    }
}
