using PhotonBypass.Domain.Session.Entity;

namespace PhotonBypass.Domain.Session.Model;

public class CertEmailContext : CertContext
{
    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;
}
