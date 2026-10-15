using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Xui.WebService;

/// <summary>
/// x-ui panel API contract. Coded against the assumed alireza0 v1.12.0 contract
/// and verified by tests through mocks until the vm01 probe (docs/17) confirms
/// the real endpoint and field encodings.
/// </summary>
public interface IXuiApi
{
    Task<List<XuiClientModel>> ListClients(ServerEntity server);

    Task<List<XuiClientTrafficModel>> ListClientTraffic(ServerEntity server);

    Task<XuiClientModel?> GetClient(ServerEntity server, string email);

    Task AddClient(ServerEntity server, XuiClientModel client);

    Task UpdateClient(ServerEntity server, XuiClientModel client);

    Task RemoveClient(ServerEntity server, string email);

    Task ResetClientTraffic(ServerEntity server, string email);
}
