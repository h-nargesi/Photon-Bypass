using PhotonBypass.Domain.Account.Model;

namespace PhotonBypass.Application.Account.Model;

/// <summary>
/// Admin/manager user-creation payload (docs/20 phase 4). <see cref="Role"/>
/// and <see cref="Owner"/> are admin-only; a manager always creates its own
/// subordinate <see cref="AccountRole.User"/>.
/// </summary>
public class CreateUserModel : RegisterModel
{
    public bool AllowSelfService { get; set; }

    /// <summary>Username of the owning manager (admin only; attach is manager-only, docs/20 §3).</summary>
    public string? Owner { get; set; }

    /// <summary>Requested role (admin only; Admin is refused — D9 has no promotion path).</summary>
    public AccountRole? Role { get; set; }
}
