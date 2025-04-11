using PhotonBypass.Application.Account.Model;

namespace PhotonBypass.Application.Authentication.Model;

public class RegisterContext : EditUserContext
{
    public string? Username { get; set; }

    public string? Password { get; set; }
}
