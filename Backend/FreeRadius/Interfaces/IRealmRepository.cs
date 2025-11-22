using PhotonBypass.FreeRadius.Entity;

namespace PhotonBypass.FreeRadius.Interfaces;

public interface IRealmRepository
{
    Task<RealmEntity?> Fetch(int realm_id);

    Task<List<ServerDensityEntity>> FetchServerDensityEntity(int cloud_id);
}
