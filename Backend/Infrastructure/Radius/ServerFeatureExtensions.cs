using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;

namespace PhotonBypass.Infra.Radius;

public static class ServerFeatureExtensions
{
    public static string? GetSyncKey(this ServerEntity server)
    {
        return (server.Features & ServerFeature.Radius) switch
        {
            ServerFeature.UserManager => RadiusType.UserManager,
            ServerFeature.RadiusDesk => RadiusType.RadiusDesk,
            ServerFeature.Xui => RadiusType.Xui,
            _ => null,
        };
    }
}
