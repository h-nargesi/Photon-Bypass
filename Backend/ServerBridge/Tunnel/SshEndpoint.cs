namespace PhotonBypass.ServerBridge.Tunnel;

/// <summary>
/// Everything needed to open one SSH session. Built from the server row by
/// <see cref="SshEndpointFactory"/>; it carries secrets (key, passphrase,
/// password) and must never be logged or put into an exception message.
/// </summary>
internal sealed record SshEndpoint(
    string Host, int Port, string Username, string? Password, string? PrivateKey,
    string? PassPhrase, string? HostKeySha256, HostKeyPolicy HostKeyPolicy,
    TimeSpan ConnectTimeout, TimeSpan KeepAlive)
{
    /// <summary>
    /// Identity of the SSH leg (host, port, username, pin, hash of the private
    /// key): a pooled session whose fingerprint no longer matches the row is
    /// rebuilt on the next use.
    /// </summary>
    public string Fingerprint { get; init; } = string.Empty;
}
