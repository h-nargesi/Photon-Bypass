namespace PhotonBypass.Domain.Account.Model;

public class EditUserModel
{
    public string? Email { get; set; }

    public string? Mobile { get; set; }

    public string? Firstname { get; set; }

    public string? Lastname { get; set; }
}

public class FullUserModel : EditUserModel
{
    public string Username { get; set; } = null!;

    public bool EmailValid { get; set; }

    public bool MobileValid { get; set; }
}
