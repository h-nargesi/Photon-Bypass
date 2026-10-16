namespace PhotonBypass.Domain.Account.Model;

/// <summary>
/// Account role for the admin/manager panel (docs/20 D2/D9).
/// Admin is exactly the one seeded account; there is no promotion path.
/// </summary>
public enum AccountRole : byte
{
    User = 0,

    Manager = 1,

    Admin = 2,
}
