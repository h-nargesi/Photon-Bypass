using PhotonBypass.Domain.Repository;
using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Domain.Servers;

public interface IRealmRepository : IEditableRepository<RealmEntity>
{
    Task<string?> GetName(int realm_id);

    Task<List<RealmEntity>> FetchAllActiveRealm();
}