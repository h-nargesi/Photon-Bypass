namespace PhotonBypass.Domain.Session.Entity;

public class CertContext
{
    public string Realm { get; set; } = null!;

    public string PrivateKeyOvpn { get; set; } = null!;

    public byte[] CertFile { get; set; } = null!;
}
