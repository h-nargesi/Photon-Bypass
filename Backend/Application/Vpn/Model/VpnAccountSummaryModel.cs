namespace PhotonBypass.Application.Vpn.Model;

/// <summary>
/// One row of the account switcher (Docs/22 §3.5): the visible VPN accounts
/// of the acting user (or of a manager's subordinate).
/// </summary>
public class VpnAccountSummaryModel
{
    public int Id { get; init; }

    public string Username { get; init; } = null!;

    public int RealmId { get; init; }

    public string? RealmName { get; init; }

    /// <summary>Raw <see cref="PhotonBypass.Domain.Account.Model.VpnAccountStatus"/> value (0/1/2).</summary>
    public int Status { get; init; }

    public DateTime Created { get; init; }
}
