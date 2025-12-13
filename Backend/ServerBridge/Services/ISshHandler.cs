using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.ServerBridge.Services;

public interface ISshHandler
{
    Task<ISshConnection> ConnectTo(ServerEntity server);
}
