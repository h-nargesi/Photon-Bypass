using PhotonBypass.Domain.Servers.Entity;
using Renci.SshNet;

namespace PhotonBypass.ServerBridge.Services;

public interface ISshHandler
{
    Task<SshClient> ConnectTo(ServerEntity server);
}
