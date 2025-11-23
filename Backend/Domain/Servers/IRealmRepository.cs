using PhotonBypass.Domain.Servers.Model;

namespace PhotonBypass.Domain.Servers;

public interface IRealmRepository
{
    Task<RealmEntity?> Fetch(int realm_id);

    Task<List<RealmEntity>> FetchServerDensityEntity();
}