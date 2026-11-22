using PhotonBypass.Domain.Account.Model;

namespace PhotonBypass.Application.Account.Model;

/// <summary>
/// Admin/manager user-creation payload (Docs/20 phase 4). <see cref="Role"/>
/// and <see cref="Owner"/> are admin-only; a manager always creates its own
/// subordinate <see cref="AccountRole.User"/>.
/// </summary>
public class CreateUserModel : RegisterModel
{
    public bool AllowSelfService { get; set; }

    /// <summary>Username of the owning manager (admin only; attach is manager-only, Docs/20 §3).</summary>
    public string? Owner { get; set; }

    /// <summary>Requested role (admin only; Admin is refused — D9 has no promotion path).</summary>
    public AccountRole? Role { get; set; }

    /// <summary>
    /// Docs/24 D6: explicit pricing-group override (admin only). Without it the
    /// group resolves from the attach rules — the manager's group, or the
    /// default group for independent users.
    /// </summary>
    public int? GroupId { get; set; }
}
