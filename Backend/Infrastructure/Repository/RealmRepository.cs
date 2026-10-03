using PhotonBypass.Domain.Plan.Business;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class RealmRepository(LocalDbContext context) : EditableRepository<RealmEntity>(context), IRealmRepository
{
    public async Task<string?> GetName(int id)
    {
        var sql = $"""
                   select {nameof(RealmEntity.Name)}
                   from {TableName}
                   where {nameof(RealmEntity.Id)} = @id
                   """;

        var result = await QueryAsync<string>(sql, new { id });

        return result.FirstOrDefault();
    }

    public async Task<List<RealmEntity>> FetchAllActiveRealm()
    {
        var result = await FindAsync(statement => statement.Where($"{nameof(RealmEntity.IsActive)} = 1"));

        return [.. result];
    }

    public async Task<Dictionary<int, RealmEntity>> GetByIds(List<int> ids)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(RealmEntity.Id)} in @ids")
            .WithParameters(new { ids }));

        return result.ToDictionary(realm => realm.Id);
    }

    public async Task<List<int>> TryLockTrafficSync(IEnumerable<int> ids)
    {
        var now = DateTime.Now;
        var limit = now.AddSeconds(-RenewalBusiness.UpdateTrafficDataTimeSecondLimit);

        var sql = $"""
                   update {TableName}
                   set {nameof(RealmEntity.LastTrafficSync)} = @now
                   output inserted.{nameof(RealmEntity.Id)}
                   where {nameof(RealmEntity.Id)} in @ids
                     and ({nameof(RealmEntity.LastTrafficSync)} is null
                          or {nameof(RealmEntity.LastTrafficSync)} < @limit)
                   """;

        var result = await QueryAsync<int>(sql, new { ids, now, limit });

        return [.. result];
    }
}