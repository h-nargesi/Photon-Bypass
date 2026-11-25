using PhotonBypass.Domain.Servers.Entity;

namespace PhotonBypass.Domain.Servers;

public static class RealmRepositoryExtensions
{
    /// <summary>
    /// Realms by id. An empty id set answers with an empty dictionary instead
    /// of querying the database.
    /// </summary>
    public static async Task<Dictionary<int, RealmEntity>> LoadRealms(
        this IRealmRepository repository,
        IEnumerable<int> realm_ids)
    {
        var ids = realm_ids.Distinct().ToList();

        return ids.Count == 0 ? [] : await repository.GetByIds(ids);
    }
}
