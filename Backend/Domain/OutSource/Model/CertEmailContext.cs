using PhotonBypass.Domain.Plan.Entity;

namespace PhotonBypass.Domain.Plan.Model;

public class CertEmailContext : CertContext
{
    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;
}
