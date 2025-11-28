using PhotonBypass.Domain.Repository;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;

namespace PhotonBypass.Domain.Servers;

public interface IServerRepository : IEditableRepository<ServerEntity>
{
    Task<ServerEntity?> GetActiveNasInfo(string ip);

    Task<List<string>> GetAllActiveNasDomainInRealm(int? realm_id);

    Task<List<ServerEntity>> GetAllActiveRadius();

    Task<List<ServerEntity>> GetActiveRadiusInRealmOrAll(int? realm_id);

    Task<Dictionary<int, List<ServerEntity>>> GetAllActiveNasInRealm(IEnumerable<int> realm_ids);
}
