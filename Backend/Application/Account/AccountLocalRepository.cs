using PhotonBypass.Domain.User;
using PhotonBypass.Infra.Database;

namespace PhotonBypass.Application.Account;

class AccountLocalRepository(LocalDbContext context) : LocalRepository<UserLocalEntity>(context)
{
    public async Task<UserLocalEntity?> GetUser(string username)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(UserLocalEntity.Username)} = @username")
            .WithParameters(new { username }));

        return result.FirstOrDefault();
    }

    public async Task<IList<UserLocalEntity>> GetTargetArea(int userid)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(UserLocalEntity.Parent)} = @userid")
            .WithParameters(new { userid }));

        return result.ToList();
    }
}
