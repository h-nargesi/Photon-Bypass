using PhotonBypass.Domain.Servers.Model;

namespace PhotonBypass.Domain.Servers;

public interface INasRepository
{
    Task<List<NasEntity>> GetAll();

    Task<NasEntity?> GetNasInfo(string ip);

    Task<List<NasEntity>> GetAllInRealm(int realm_id);

    Task<Dictionary<string, NasEntity>> GetNasInfo(IEnumerable<string> ips);
}
