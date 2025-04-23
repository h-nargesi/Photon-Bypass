using Dapper.FastCrud;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Infra.Database;
using PhotonBypass.Radius.Repository.DbContext;
using PhotonBypass.Tools;

namespace PhotonBypass.Radius.Repository;

class RealmRepository(RadDbContext context) : DapperRepository<RealmEntity>(context), IRealmRepository
{
    readonly static string Id = EntityExtensions.GetColumnName<RealmEntity>(x => x.Id);

    public async Task<RealmEntity?> Fetch(int realm_id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{Id} = @realm_id")
            .WithParameters(new { realm_id }));

        return result.FirstOrDefault();
    }

    public async Task<IList<RealmEntity>> FetchAll(int cloud_id)
    {
        var result = await connection.FindAsync<RealmEntity>();

        return [.. result];
    }
}
