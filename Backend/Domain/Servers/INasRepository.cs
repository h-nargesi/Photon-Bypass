using PhotonBypass.Domain.Repository;
using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Domain.Servers;

public interface INasRepository : IEditableRepository<NasEntity>
{
    Task<List<NasEntity>> GetAllActive();

    Task<NasEntity?> GetActiveNasInfo(string ip);

    Task<Dictionary<string, NasEntity>> GetNasInfo(IEnumerable<string> ips);

    Task<List<NasEntity>> GetAllActiveInRealm(int realm_id);

    Task<List<string>> GetAllActiveDomainInRealm(int? realm_id);

    Task<Dictionary<int, List<NasEntity>>> GetAllActiveInRealm(IEnumerable<int> realm_ids);
}
