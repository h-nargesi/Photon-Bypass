namespace PhotonBypass.Domain.Servers.JsonType;


public class ServerConfiguration
{
    public SshConfig? SshConfig { get; set; }

    public WebApiConfig? WebApiConfig { get; set; }
    
    public DataBaseInfo? DataBaseInfo { get; set; }
}
