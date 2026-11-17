using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PhotonBypass.Domain.Account.Business;
using PhotonBypass.Domain.Account.Model;

namespace PhotonBypass.Domain.Account.Entity;

/// <summary>
/// Panel-user half of the old <c>Account</c> split (Docs/22 D1): portal
/// login, identity and wallet live here; every VPN identity lives on a
/// <see cref="VpnAccountEntity"/> owned by this user.
/// </summary>
[Table("PanelUser")]
public class PanelUserEntity : IBaseEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>Portal-only status (Docs/22 D12): 0=Active, 2=Deactivated (login blocked).</summary>
    public PanelUserStatus Status { get; set; } = PanelUserStatus.Active;

    public string Username { get; init; } = null!;

    public string Password { get; set; } = null!;

    /// <summary>Manager this user is a subordinate of (Docs/20 §3); null = independent.</summary>
    public int? Owner { get; set; }

    public DateTime Created { get; init; } = DateTime.Now;

    // Account Personal Info

    public string? Name { get; set; }

    public string? Surname { get; set; }

    [NotMapped]
    public string Fullname => (Name + " " + Surname).Trim();

    public string? Mobile { get; set; }

    public bool IsMobileValid { get; set; }

    [NotMapped]
    public string? MobileNumber => IsMobileValid ? Mobile : null;

    public string? Email { get; set; }

    public bool IsEmailValid { get; set; }

    [NotMapped]
    public string? EmailAddress => IsEmailValid ? Email : null;

    // Telegram: the chat bound through the one-time deep-link flow; null = not
    // connected. No validity flag — unlike email/mobile, reaching this column
    // means the chat authenticated itself via the bot link.
    public long? TelegramChatId { get; set; }

    [NotMapped]
    public string Picture => AccountBusiness.PicturePath + Id;

    // Profile Info

    /// <summary>Price calculation method (Docs/22 D10: realm-independent, user-level).</summary>
    public int? CalculationMethod { get; set; }

    public AccountRole Role { get; set; }

    public UserTypes UserType { get; set; }
}
