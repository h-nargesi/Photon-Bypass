using PhotonBypass.Domain.Profile;
using PhotonBypass.Infra.Database.Radius;

namespace PhotonBypass.Application.Database;

class PermenantUsersRepository(IRadRepository<PermenantUserEntity> repository)
{
    public async Task<PermenantUserEntity?> GetUser(string username)
    {
        var result = await repository.FindAsync(statement => statement
            .Where($"{nameof(PermenantUserEntity.Username)} = @username")
            .WithParameters(new { username }));

        return result.FirstOrDefault();
    }

    public async Task<bool> CheckUsername(string username)
    {
        var result = await repository.FindAsync(statement => statement
            .Where($"{nameof(PermenantUserEntity.Username)} = @username")
            .WithParameters(new { username }));

        return result.Any();
    }
}
