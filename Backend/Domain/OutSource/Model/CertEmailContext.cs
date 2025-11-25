using PhotonBypass.Domain.Account.Entity;

namespace PhotonBypass.Domain.Account.Model;

public class CertEmailContext : CertContext
{
    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;
}
