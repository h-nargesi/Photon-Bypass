using PhotonBypass.Domain.Repository;
using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Domain.Servers;

public interface IRealmRepository : IEditableRepository<RealmEntity>
{
    Task<string?> GetName(int id);

    Task<List<RealmEntity>> FetchAllActiveRealm();

    Task<Dictionary<int, RealmEntity>> GetByIds(List<int> ids);
}