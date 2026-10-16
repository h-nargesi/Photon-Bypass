namespace PhotonBypass.Domain.Account.Model;

/// <summary>
/// One row of the manager/admin user list (docs/20 phase 4): account scalars
/// (inactive accounts included) plus the latest plan summary joined from the
/// <c>TotalPlanState</c> view.
/// </summary>
public class AccountOverviewModel
{
    public string Username { get; set; } = null!;

    public string? Name { get; set; }

    public string? Surname { get; set; }

    public string? Email { get; set; }

    public AccountRole Role { get; set; }

    public bool AllowSelfService { get; set; }

    public bool IsActive { get; set; }

    /// <summary>Username of the owning account; null when independent.</summary>
    public string? Owner { get; set; }

    public DateTime Created { get; set; }

    // Latest plan summary (null when the account never renewed)

    public byte? SimultaneousUser { get; set; }

    public DateTime? ExpirationDate { get; set; }

    public long? TrafficLeft { get; set; }

    public DateTime? LastConnectTime { get; set; }
}
