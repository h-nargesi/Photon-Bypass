using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class RealmRepository(LocalDbContext context) : EditableRepository<RealmEntity>(context), IRealmRepository
{
    public Task<string?> GetName(int realm_id)
    {
        throw new NotImplementedException();
    }

    public Task<List<RealmEntity>> FetchAllActiveRealm()
    {
        throw new NotImplementedException();
    }
}