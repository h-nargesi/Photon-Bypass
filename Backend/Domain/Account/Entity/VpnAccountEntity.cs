using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PhotonBypass.Domain.Account.Model;

namespace PhotonBypass.Domain.Account.Entity;

/// <summary>
/// VPN half of the old <c>Account</c> split (Docs/22 D1): one panel user owns
/// N VPN accounts (D2), each pinned to exactly one realm. The username is
/// globally unique (D3) and starts with the owner's panel username.
/// </summary>
[Table("VpnAccount")]
public class VpnAccountEntity : IBaseEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public int UserId { get; set; }

    /// <summary>The realm this account is provisioned on (Docs/22 D5/D6); never null.</summary>
    public int RealmId { get; set; }

    /// <summary>Globally-unique VPN username (Docs/22 D3): owner panel username + optional suffix.</summary>
    public string Username { get; init; } = null!;

    public string VpnPassword { get; set; } = null!;

    /// <summary>VPN-level status (Docs/22 D12): 0=Active, 1=Suspended (VPN blocked, portal alive), 2=Deactivated.</summary>
    public VpnAccountStatus Status { get; set; } = VpnAccountStatus.Active;

    public bool SendWarning { get; set; } = true;

    public DateTime? LastWarningTime { get; set; }

    // Stamp of the last x-ui UUID rotation (Docs/16 D4): backs the per-account
    // throttle, exactly like LastWarningTime backs the warning throttle.
    public DateTime? LastCredentialRotateTime { get; set; }

    public DateTime Created { get; init; } = DateTime.Now;
}
