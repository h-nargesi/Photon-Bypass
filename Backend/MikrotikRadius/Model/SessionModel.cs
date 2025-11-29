using System.Text.Json.Serialization;

namespace PhotonBypass.Mikrotik.Radius.Model;

public class SessionModel
{
    [JsonPropertyName("user")]
    public string Username { get; set; }
    
    [JsonPropertyName("acct-session-id")]
    public string SessionId { get; set; }
    
    [JsonPropertyName("nas-ip-address")]
    public string NasIpAddress { get; set; }
    
    [JsonPropertyName("calling-station-id")]
    public string CallerId { get; set; }
    
    [JsonPropertyName("started")]
    public DateTime Started { get; set; }
    
    [JsonPropertyName("ended")]
    public DateTime Ended { get; set; }
    
    [JsonPropertyName("uptime")]
    public TimeSpan UpTime { get; set; }
    
    [JsonPropertyName("download")]
    public long Download { get; set; }
    
    [JsonPropertyName("upload")]
    public long Upload { get; set; }
}