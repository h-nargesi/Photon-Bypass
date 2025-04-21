namespace PhotonBypass.Domain.Services;

public class CertContext
{
    public string Server { get; set; } = null!;

    public string PrivateKey { get; set; } = null!;

    public object[] CertFile { get; set; } = null!;
}
