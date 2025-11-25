namespace PhotonBypass.Domain.Session.Entity;

public class RegisterModel : EditUserModel
{
    public string? Username { get; set; }

    public string? Password { get; set; }
}
