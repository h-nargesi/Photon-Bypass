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
}