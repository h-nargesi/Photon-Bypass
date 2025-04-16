namespace PhotonBypass.Application.Connection.Model;

public class ConnectionStateModel
{
    public int Duration { get; set; }

    public ConnectionState State { get; set; }

    public string Server { get; set; } = null!;

    public string SessionId { get; set; } = null!;
}

public enum ConnectionState
{
    Up, Down
}