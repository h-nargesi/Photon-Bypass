namespace PhotonBypass.Domain.Services.Model;

public class CertEmailContext : CertContext
{
    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;
}
