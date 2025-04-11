namespace PhotonBypass.Domain.Model.User;

public class EditUserModel
{
    public string Email { get; set; } = string.Empty;

    public string Mobile { get; set; } = string.Empty;

    public string Firstname { get; set; } = string.Empty;

    public string Lastname { get; set; } = string.Empty;
}

public class FullUserModel : EditUserModel
{
    public string Username { get; set; } = string.Empty;

    public bool EmailValid { get; set; }

    public bool MobileValid { get; set; }
}
