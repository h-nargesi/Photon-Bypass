namespace PhotonBypass.Domain.OutSource.Model;

/// <summary>
/// Payload of the unified connection-delivery email: one request builds every
/// connection artifact the account owns (x-ui vless link and/or OpenVPN
/// certificate, docs/16 D4) and sends it in a single message; template
/// sections whose value is missing are stripped from the body.
/// </summary>
public class ConnectionEmailContext
{
    public string Username { get; set; } = null!;

    public string? Password { get; set; }

    public string? Link { get; set; }

    public string? PrivateKeyOvpn { get; set; }

    public byte[]? CertFile { get; set; }
}
