namespace PhotonBypass.Domain.Profile;

public interface IRealmRepository
{
    Task<RealmEntity?> Fetch(int realm_id);

    Task<IList<RealmEntity>> FetchAll(int cloud_id);
}
