using System.Text.Json.Serialization;

namespace PhotonBypass.Domain.Servers.JsonType;

public class WebApiConfig
{
    public string? HostName { get; set; }

    public int? Port { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public bool Ssl { get; set;  }

    [JsonIgnore]
    public string HttpsUrl => $"https://{HostName}{(Port.HasValue ? $":{Port}" : "")}";
}