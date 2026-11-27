namespace PhotonBypass.Domain.Servers.JsonType;

public class SshConfig
{
    public string Username { get; set; } = null!;

    /// <summary>
    /// SSH password of the <c>Direct</c> transport; null on a <c>SshTunnel</c>
    /// row, whose SSH leg is authenticated with <see cref="PrivateKey"/>.
    /// </summary>
    public string? Password { get; set; }

    public int Port { get; set; } = 22;

    /// <summary>Full text of the private key (OpenSSH format) — required in <c>SshTunnel</c>.</summary>
    public string? PrivateKey { get; set; }

    /// <summary>Optional passphrase of <see cref="PrivateKey"/>.</summary>
    public string? PassPhrase { get; set; }

    /// <summary>Pinned host key fingerprint, <c>SHA256:</c> + unpadded base64 — required in <c>SshTunnel</c>.</summary>
    public string? HostKeySha256 { get; set; }
}
