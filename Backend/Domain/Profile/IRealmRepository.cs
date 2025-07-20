namespace PhotonBypass.Domain.Profile;

public interface IRealmRepository
{
    Task<RealmEntity?> Fetch(int realm_id);

    Task<List<ServerDensityEntity>> FetchServerDensityEntity(int cloud_id);
}
