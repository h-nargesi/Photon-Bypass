namespace PhotonBypass.Domain.Account.Model;

/// <summary>
/// Cached login context of a user (docs/20 D2, docs/22 §3): the role for
/// authorization checks, the usernames the user may act on behalf of and the
/// VPN account ids the user may operate. The account set is only trusted
/// after a repository rebuild — the login snapshot can go stale the moment
/// the user buys another account (docs/22 D9), so <see cref="AccountIds"/>
/// from <c>LoginEvent</c> is a warm start, never the truth.
/// </summary>
public class TargetAreaModel
{
    public AccountRole Role { get; set; }

    public HashSet<string> Targets { get; set; } = [];

    /// <summary>VPN account ids reachable by this user (own + subordinates'; admin bypasses).</summary>
    public HashSet<int> AccountIds { get; set; } = [];

    /// <summary>False while AccountIds is just the login snapshot (may miss fresh purchases).</summary>
    public bool AccountIdsTrusted { get; set; }
}
