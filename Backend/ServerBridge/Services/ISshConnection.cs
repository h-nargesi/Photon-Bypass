using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.ServerBridge.Services;

public interface ISshConnection : IDisposable
{
    ServerEntity Server { get; }

    bool Execute(string command, out string result);
}
