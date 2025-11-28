namespace PhotonBypass.Domain.Servers.JsonType;

public class SshConfig
{
    public string Username { get; set; } = null!;

    public string Password { get; set; } = null!;

    public int Port { get; set; } = 22;
}