namespace PhotonBypass.Domain.Server;

public class UserConnectionBinding
{
    public string Name { get; set; } = null!;

    public string CallerId { get; set; } = null!;

    public string UpTime { get; set; } = null!;

    public string SessionId { get; set; } = null!;
}
