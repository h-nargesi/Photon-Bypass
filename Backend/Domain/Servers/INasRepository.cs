using PhotonBypass.Domain.Repository;
using PhotonBypass.Domain.Servers.Types;

namespace PhotonBypass.Domain.Servers;

public interface INasRepository : IEditableRepository<NasEntity>
{
    Task<List<NasEntity>> GetAll();

    Task<NasEntity?> GetNasInfo(string ip);

    Task<Dictionary<string, NasEntity>> GetNasInfo(IEnumerable<string> ips);

    Task<List<NasEntity>> GetAllInRealm(int realm_id);

    Task<List<string>> GetAllDomainInRealm(int realm_id);

    Task<Dictionary<int, List<NasEntity>>> GetAllInRealm(IEnumerable<int> realm_ids);
}
