using PhotonBypass.Domain.Services;

namespace PhotonBypass.Application.Connection.Model;

public class ConnectionStateModel
{
    public string SessionId { get; set; } = null!;

    public string Server { get; set; } = null!;
    
    public int Duration { get; set; }

    public ConnectionState State { get; set; }
}
