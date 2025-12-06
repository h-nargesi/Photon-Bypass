using PhotonBypass.Domain.Servers.Entity;
using tik4net;

namespace PhotonBypass.ServerBridge.Services;

public interface ITik4NetHandler
{
    Task<ITikConnection> ConnectTo(ServerEntity server);
}
