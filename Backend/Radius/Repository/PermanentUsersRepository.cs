using PhotonBypass.Domain.Profile;
using PhotonBypass.Infra.Database;
using PhotonBypass.Radius.Repository.DbContext;

namespace PhotonBypass.Radius.Repository;

class PermanentUsersRepository(RadDbContext context) : DapperRepository<PermanentUserEntity>(context), IPermanentUsersRepository
{
    public async Task<PermanentUserEntity?> GetUser(string username)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(PermanentUserEntity.Username)} = @username and Active = 1")
            .WithParameters(new { username }));

        return result.FirstOrDefault();
    }

    public async Task<bool> CheckUsername(string username)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(PermanentUserEntity.Username)} = @username and Active = 1")
            .WithParameters(new { username }));

        return result.Any();
    }

    public Task<string> GetRestrictedServer(string username)
    {
        throw new NotImplementedException();
    }

    public Task<UserPlanStateEntity?> GetPlanState(string username)
    {
        throw new NotImplementedException();
    }
}
