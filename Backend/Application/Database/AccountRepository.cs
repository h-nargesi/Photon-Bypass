using PhotonBypass.Domain.User;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Database.Local;

namespace PhotonBypass.Application.Database;

class AccountRepository(ILocalRepository<AccountEntity> repo) : EditableRepository<AccountEntity>(repo)
{
    public async Task<AccountEntity?> GetAccount(string username)
    {
        var result = await repository.FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Username)} = @username")
            .WithParameters(new { username }));

        return result.FirstOrDefault();
    }

    public async Task<IList<AccountEntity>> GetTargetArea(int userid)
    {
        var result = await repository.FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Parent)} = @userid")
            .WithParameters(new { userid }));

        return result.ToList();
    }
}
