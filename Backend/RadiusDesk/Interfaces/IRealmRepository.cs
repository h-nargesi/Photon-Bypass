using PhotonBypass.RadiusDesk.Entity;

namespace PhotonBypass.RadiusDesk.Interfaces;

public interface IRealmRepository
{
    Task<RealmEntity?> Fetch(int realm_id);

    Task<List<ServerDensityEntity>> FetchServerDensityEntity(int cloud_id);
}
