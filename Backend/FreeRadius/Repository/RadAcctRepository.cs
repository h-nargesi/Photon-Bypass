using PhotonBypass.FreeRadius.Entity;
using PhotonBypass.FreeRadius.Interfaces;
using PhotonBypass.FreeRadius.Repository.DbContext;
using PhotonBypass.Infra.Database;
using PhotonBypass.Tools;

namespace PhotonBypass.FreeRadius.Repository;

class RadAcctRepository(RadDbContext context) : DapperRepository<RadAcctEntity>(context), IRadAcctRepository
{
    private static readonly string Username = EntityExtensions.GetColumnName<RadAcctEntity>(x => x.Username);
    private static readonly string AcctStopTime = EntityExtensions.GetColumnName<RadAcctEntity>(x => x.AcctStopTime);
    private static readonly string AcctStartTime = EntityExtensions.GetColumnName<RadAcctEntity>(x => x.AcctStartTime);

    public async Task<IList<RadAcctEntity>> GetCurrentConnectionList(string username)
    {
        var result = await FindAsync(statement => statement
            .Where($"{Username} = @username and {AcctStopTime} is null")
            .OrderBy($"{AcctStartTime}")
            .WithParameters(new { username }));

        return [.. result];
    }
}
