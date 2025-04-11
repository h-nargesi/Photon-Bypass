namespace PhotonBypass.Domain.Model.User;

public class ChangePasswordContext
{
    public string? Token { get; set; }

    public string? Password { get; set; }
}
