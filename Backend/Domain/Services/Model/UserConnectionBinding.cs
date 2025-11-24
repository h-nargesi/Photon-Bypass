namespace PhotonBypass.Domain.Services.Model;

public class UserConnectionBinding
{
    public string SessionId { get; set; } = null!;
    
    public string Name { get; set; } = null!;

    public string CallerId { get; set; } = null!;

    public TimeSpan UpTime { get; set; }

    public ConnectionState State { get; set; }
}

public enum ConnectionState
{
    Up, Down
}
