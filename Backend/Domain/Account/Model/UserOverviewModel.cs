namespace PhotonBypass.Domain.Account.Model;

/// <summary>
/// One row of the manager/admin user list (Docs/20 phase 4, Docs/22 D7):
/// panel-user scalars (inactive users included). The VPN accounts of each
/// user load separately and nest under the row.
/// </summary>
public class UserOverviewModel
{
    public string Username { get; set; } = null!;

    public string? Name { get; set; }

    public string? Surname { get; set; }

    public string? Email { get; set; }

    public AccountRole Role { get; set; }

    public bool AllowSelfService { get; set; }

    public PanelUserStatus Status { get; set; }

    /// <summary>Username of the owning manager; null when independent.</summary>
    public string? Owner { get; set; }

    public DateTime Created { get; set; }
}

/// <summary>
/// One VPN-account row nested under a user row (Docs/22 §3.6): account
/// scalars plus the latest plan summary joined from <c>PlanState</c>.
/// </summary>
public class UserAccountOverviewModel
{
    /// <summary>Panel username of the owner — the nesting key of the user list.</summary>
    public string Owner { get; set; } = null!;

    public int UserId { get; set; }

    public int AccountId { get; set; }

    public string Username { get; set; } = null!;

    public int RealmId { get; set; }

    public string? RealmName { get; set; }

    public VpnAccountStatus Status { get; set; }

    public DateTime Created { get; set; }

    // Latest plan summary (null when the account never renewed)

    public byte? SimultaneousUser { get; set; }

    public DateTime? ExpirationDate { get; set; }

    public long? TrafficLeft { get; set; }

    public DateTime? LastConnectTime { get; set; }
}
