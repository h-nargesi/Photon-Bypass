namespace PhotonBypass.Domain.Model.User;

public class RegisterContext : EditUserContext
{
    public string? Username { get; set; }

    public string? Password { get; set; }
}
