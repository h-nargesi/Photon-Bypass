using Microsoft.Extensions.Caching.Memory;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Model;

namespace PhotonBypass.Portal.Basical;

internal class AccessService(IMemoryCache cache, Lazy<IAccountRepository> account_repo) : IAccessService
{
    public async Task<bool> CheckAccess(string username, string target)
    {
        var area = cache.Get<TargetAreaModel>($"TargetArea|{username}");

        if (area == null)
        {
            area = await RebuildTargetArea(username);

            if (area == null)
            {
                return false;
            }

            LoginEvent(username, area.Role, area.Targets);
        }

        return area.Role == AccountRole.Admin || area.Targets.Contains(target);
    }

    public void LoginEvent(string username, AccountRole role, IEnumerable<string>? area)
    {
        cache.Set($"TargetArea|{username}", new TargetAreaModel
        {
            Role = role,
            Targets = [.. (area ?? [])],
        }, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2),
            Size = 1,
        });
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
    /// Rebuilds the login area from the database with the same rules the
    /// login path applies (docs/14 M32): admins cover every active account,
    /// everyone else exactly their active subordinates. Unknown or inactive
    /// accounts are not rebuilt — access stays denied.
    /// </summary>
    private async Task<TargetAreaModel?> RebuildTargetArea(string username)
    {
        var account = await account_repo.Value.GetAccount(username);

        if (account is not { IsActive: true })
        {
            return null;
        }

        var area_accounts = account.Role == AccountRole.Admin
            ? await account_repo.Value.GetAllActive()
            : await account_repo.Value.GetActiveTargetArea(account.Id);

        return new TargetAreaModel
        {
            Role = account.Role,
            Targets = [.. area_accounts.Select(active => active.Username)],
        };
    }
}
