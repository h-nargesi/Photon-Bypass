using PhotonBypass.Domain.Profile;
using PhotonBypass.Infra.Database;
using PhotonBypass.Radius.Repository.DbContext;

namespace PhotonBypass.Radius.Repository;

class RadAcctRepository(RadDbContext context) : DapperRepository<RadAcctEntity>(context), IRadAcctRepository
{
    public async Task<IList<RadAcctEntity>> GetCurrentConnectionList(string username)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(RadAcctEntity.Username)} = @username and {nameof(RadAcctEntity.AcctStopTime)} is null")
            .OrderBy($"{nameof(RadAcctEntity.AcctStartTime)}")
            .WithParameters(new { username }));

        return result.ToList();
    }
}
