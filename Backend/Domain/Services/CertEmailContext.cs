namespace PhotonBypass.Domain.Services;

public class CertEmailContext
{
    public string Server { get; set; } = null!;

    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;

    public string PrivateKey { get; set; } = null!;

    public object[] CertFile { get; set; } = null!;
}
