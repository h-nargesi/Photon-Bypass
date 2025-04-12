using PhotonBypass.Domain.User;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Database.Radius;

namespace PhotonBypass.Application.Database;

internal class PermenantUserRepository(IRadRepository<PermenantUserEntity> repo) : EditableRepository<PermenantUserEntity>(repo)
{
    public async Task<PermenantUserEntity?> GetUser(string username)
    {
        var result = await repository.FindAsync(statement => statement
            .Where($"{nameof(PermenantUserEntity.Username)} = @username")
            .WithParameters(new { username }));

        return result.FirstOrDefault();
    }
}
