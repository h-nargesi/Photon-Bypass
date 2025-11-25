namespace PhotonBypass.Domain.Account.Model;

public class RegisterModel : EditUserModel
{
    public string? Username { get; set; }

    public string? Password { get; set; }
}
