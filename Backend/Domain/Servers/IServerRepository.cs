using PhotonBypass.Domain.Repository;
using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Domain.Servers;

public interface IServerRepository : IEditableRepository<ServerEntity>
{
    Task<ServerEntity?> GetActiveNasInfo(string ip);

    Task<List<string>> GetAllActiveNasDomainInRealm(int? realm_id);

    Task<Dictionary<int, List<ServerEntity>>> GetAllActiveRadiusInRealm(IEnumerable<int> realm_ids);
}
