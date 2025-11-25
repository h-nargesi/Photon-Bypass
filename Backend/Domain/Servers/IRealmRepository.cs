using PhotonBypass.Domain.Repository;
using PhotonBypass.Domain.Servers.Types;

namespace PhotonBypass.Domain.Servers;

public interface IRealmRepository : IEditableRepository<RealmEntity>
{
    Task<RealmEntity?> Fetch(int realm_id);

    Task<List<RealmEntity>> FetchAllActiveRealm();
}