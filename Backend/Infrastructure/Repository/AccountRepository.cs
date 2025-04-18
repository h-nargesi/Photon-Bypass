using PhotonBypass.Domain.Account;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Database.Local;

namespace PhotonBypass.Application.Database;

class AccountRepository(ILocalRepository<AccountEntity> repo) : EditableRepository<AccountEntity>(repo), IAccountRepository
{
    public async Task<AccountEntity?> GetAccount(string username)
    {
        var result = await repository.FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Username)} = @username")
            .WithParameters(new { username }));

        return result.FirstOrDefault();
    }

    public async Task<AccountEntity?> GetAccountByMobile(string mobile)
    {
        var result = await repository.FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Mobile)} = @mobile")
            .WithParameters(new { mobile }));

        return result.FirstOrDefault();
    }

    public async Task<AccountEntity?> GetAccountByEmail(string email)
    {
        var result = await repository.FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Email)} = @email")
            .WithParameters(new { email }));

        return result.FirstOrDefault();
    }

    public async Task<IList<AccountEntity>> GetTargetArea(int userid)
    {
        var result = await repository.FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Parent)} = @userid")
            .WithParameters(new { userid }));

        return [.. result];
    }
}
