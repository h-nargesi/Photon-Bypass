namespace PhotonBypass.Domain.Services;

public class CertContext
{
    public string Server { get; set; } = null!;

    public string PrivateKeyOvpn { get; set; } = null!;

    public string PrivateKeyL2TP { get; set; } = null!;

    public byte[] CertFile { get; set; } = null!;
}
