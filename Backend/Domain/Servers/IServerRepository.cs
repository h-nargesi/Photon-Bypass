using PhotonBypass.Domain.Repository;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.Types;

namespace PhotonBypass.Domain.Servers;

public interface IServerRepository : IEditableRepository<ServerEntity>
{
    /// <summary>All servers ordered by id — admin CRUD list, inactive included (docs/20 phase 5).</summary>
    Task<List<ServerEntity>> GetAllServers();

    Task<ServerEntity?> GetServerById(int id);

    /// <summary>
    /// Servers already claiming the given ip/name/domain (case-insensitive),
    /// excluding <paramref name="exclude_id"/> — the create/update duplicate
    /// guard. The identity fields feed singleton lookups
    /// (<see cref="GetServerIdByIpAddress"/>), so duplicates are refused with 409.
    /// </summary>
    Task<List<ServerEntity>> FindConflicts(string ip, string name, string domain, int exclude_id);

    Task<ServerEntity?> GetActiveNasInfo(string ip);

    Task<List<string>> GetAllActiveNasDomainInRealm(int? realm_id);

    Task<List<ServerEntity>> GetActiveNasInRealmOrAll(int? realm_id);

    Task<List<ServerEntity>> GetAllActiveRadius();

    Task<List<ServerEntity>> GetActiveRadiusInRealmOrAll(int? realm_id);

    Task<Dictionary<int, List<ServerEntity>>> GetAllActiveNasInRealm(IEnumerable<int> realm_ids);

    Task<Dictionary<string, (int Id, int ReamId)>> GetServerIdByIpAddress(IEnumerable<string> ips);
}
