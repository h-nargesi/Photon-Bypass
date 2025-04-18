using PhotonBypass.Domain.Profile;
using PhotonBypass.Infra.Database;
using PhotonBypass.Radius.Repository.DbContext;

namespace PhotonBypass.Radius.Repository;

class PermenantUsersRepository(RadDbContext context) : DapperRepository<PermenantUserEntity>(context), IPermenantUsersRepository
{
    public async Task<PermenantUserEntity?> GetUser(string username)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(PermenantUserEntity.Username)} = @username")
            .WithParameters(new { username }));

        return result.FirstOrDefault();
    }

    public async Task<bool> CheckUsername(string username)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(PermenantUserEntity.Username)} = @username")
            .WithParameters(new { username }));

        return result.Any();
    }
}
