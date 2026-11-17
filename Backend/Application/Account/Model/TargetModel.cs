namespace PhotonBypass.Application.Account.Model;

public class TargetModel
{
    public string Username { get; set; } = null!;

    public string? Fullname { get; set; }

    public string? Email { get; set; }
}

/// <summary>
/// One VPN account of the logged-in user (Docs/22 §3.5): the account
/// switcher and the purchase flows render this list.
/// </summary>
public class UserVpnAccountModel
{
    public int Id { get; set; }

    public string Username { get; set; } = null!;

    public int RealmId { get; set; }

    public string? RealmName { get; set; }

    public int Status { get; set; }

    public DateTime Created { get; set; }
}
