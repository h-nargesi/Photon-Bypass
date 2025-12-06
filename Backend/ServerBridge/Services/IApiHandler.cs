using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.ServerBridge.Services;

public interface IApiHandler
{
    T LoginTo<T>(ServerEntity server, string? base_path = null);
}
