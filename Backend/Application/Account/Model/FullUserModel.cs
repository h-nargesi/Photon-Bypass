namespace PhotonBypass.Application.Account.Model;

public class EditUserContext
{
    public string? Email { get; set; }

    public string? Mobile { get; set; }

    public string? Firstname { get; set; }

    public string? Lastname { get; set; }
}

public class FullUserModel : EditUserContext
{
    public string Username { get; set; } = null!;

    public bool EmailValid { get; set; }

    public bool MobileValid { get; set; }
}
