using Microsoft.Extensions.Caching.Memory;
using PhotonBypass.Domain.Account;

namespace PhotonBypass.Portal.Basical;

internal class AccessService(IMemoryCache cache) : IAccessService
{
    public bool CheckAccess(string username, string target)
    {
        return cache.Get<HashSet<string>>($"TargetArea|{username}")
            ?.Contains(target)
            ?? false;
    }

    public void LoginEvent(string username, HashSet<string> area)
    {
        cache.Set($"TargetArea|{username}", area, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2),
            Size = 1,
        });
    }

    public void LogoutEvent(string username)
    {
        cache.Remove($"TargetArea|{username}");
    }
}
