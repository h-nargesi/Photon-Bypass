using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.ServerBridge.Services;

public interface IApiHandler
{
    string? HttpClientKey { get; set; }

    T LoginTo<T>(ServerEntity server);
}
