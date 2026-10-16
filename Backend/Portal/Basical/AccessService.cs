using Microsoft.Extensions.Caching.Memory;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Model;

namespace PhotonBypass.Portal.Basical;

internal class AccessService(IMemoryCache cache) : IAccessService
{
    public bool CheckAccess(string username, string target)
    {
        var area = cache.Get<TargetAreaModel>($"TargetArea|{username}");

        return area?.Role == AccountRole.Admin || area?.Targets.Contains(target) == true;
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
}
