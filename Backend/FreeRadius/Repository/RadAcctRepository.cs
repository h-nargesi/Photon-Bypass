using PhotonBypass.FreeRadius.Entity;
using PhotonBypass.FreeRadius.Interfaces;
using PhotonBypass.FreeRadius.Repository.DbContext;
using PhotonBypass.Infra.Database;
using PhotonBypass.Tools;

namespace PhotonBypass.FreeRadius.Repository;

class RadAcctRepository(RadDbContext context) : DapperRepository<RadAcctEntity>(context), IRadAcctRepository
{
    readonly static string Username = EntityExtensions.GetColumnName<RadAcctEntity>(x => x.Username);
    readonly static string AcctStopTime = EntityExtensions.GetColumnName<RadAcctEntity>(x => x.AcctStopTime);
    readonly static string AcctStartTime = EntityExtensions.GetColumnName<RadAcctEntity>(x => x.AcctStartTime);

    public async Task<IList<RadAcctEntity>> GetCurrentConnectionList(string username)
    {
        var result = await FindAsync(statement => statement
            .Where($"{Username} = @username and {AcctStopTime} is null")
            .OrderBy($"{AcctStartTime}")
            .WithParameters(new { username }));

        return [.. result];
    }
}
