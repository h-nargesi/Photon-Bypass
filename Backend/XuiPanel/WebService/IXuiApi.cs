using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Xui.WebService;

/// <summary>
/// x-ui panel API contract, code-verified against alireza0/x-ui v1.12.0
/// and v1.14.0 source (identical route tables and handlers for everything
/// this panel uses). Client mutations are keyed by the UUID currently live in the
/// panel (statistics are keyed by email and survive UUID changes); only
/// resetClientTraffic is keyed by email. The live vm06/vm08 probe
/// (fetch xui/API/openapi.json, docs/17 §4) gates activation.
/// </summary>
public interface IXuiApi
{
    Task<List<XuiClientModel>> ListClients(ServerEntity server);

    Task<List<XuiClientTrafficModel>> ListClientTraffic(ServerEntity server);

    Task<List<XuiOnlineClientModel>> ListOnlineClients(ServerEntity server);

    Task<XuiClientModel?> GetClient(ServerEntity server, string email);

    Task<XuiInboundModel> GetInbound(ServerEntity server);

    Task AddClient(ServerEntity server, XuiClientModel client);

    Task UpdateClient(ServerEntity server, string currentUuid, XuiClientModel client);

    Task RemoveClient(ServerEntity server, string uuid);

    Task ResetClientTraffic(ServerEntity server, string email);
}
