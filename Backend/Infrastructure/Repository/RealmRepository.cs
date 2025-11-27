using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class RealmRepository(LocalDbContext context) : EditableRepository<RealmEntity>(context), IRealmRepository
{
    public async Task<string?> GetName(int id)
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"{nameof(RealmEntity.Id)} = @id")
            .WithParameters(new { id }));

        return result.Select(r => r.Name).FirstOrDefault();
    }

    public async Task<List<RealmEntity>> FetchAllActiveRealm()
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement.Where($"{nameof(RealmEntity.IsActive)} = 1"));

        return [.. result];
    }
}