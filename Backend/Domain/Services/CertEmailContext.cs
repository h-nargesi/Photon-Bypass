namespace PhotonBypass.Domain.Services;

public class CertEmailContext : CertContext
{
    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;
}
