namespace PhotonBypass.Portal.Context;

public class RenewalContext
{
    /// <summary>VPN account being renewed (Docs/22 target contract); null on new-account requests.</summary>
    public int? AccountId { get; set; }

    /// <summary>User-level target username (manager on-behalf flows).</summary>
    public string? Target { get; set; }

    public byte? SimultaneousUserCount { get; set; }

    public short? Days { get; set; }

    public short? Gigabytes { get; set; }

    /// <summary>The re-picked realm on a stale renewal (Docs/22 D6).</summary>
    public int? RealmId { get; set; }
}

/// <summary>First-purchase payload (Docs/22 D9): realm + username + plan.</summary>
public class NewAccountContext
{
    /// <summary>User-supplied VPN username; null = accept the D3 system suggestion.</summary>
    public string? Username { get; set; }

    /// <summary>On-behalf owner username (Docs/20); null = the acting user themself.</summary>
    public string? Target { get; set; }

    public int? RealmId { get; set; }

    public byte? SimultaneousUserCount { get; set; }

    public short? Days { get; set; }

    public short? Gigabytes { get; set; }
}
