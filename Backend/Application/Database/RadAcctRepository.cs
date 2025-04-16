using PhotonBypass.Domain.Radius;
using PhotonBypass.Infra.Database.Radius;

namespace PhotonBypass.Application.Database;

class RadAcctRepository(IRadRepository<RadAcctEntity> repository)
{
    public async Task<IList<RadAcctEntity>> GetCurrentConnectionList(string username)
    {
        var result = await repository.FindAsync(statement => statement
            .Where($"{nameof(RadAcctEntity.Username)} = @username and {nameof(RadAcctEntity.AcctStopTime)} is null")
            .OrderBy($"{nameof(RadAcctEntity.AcctStartTime)}")
            .WithParameters(new { username }));

        return result.ToList();
    }
}
