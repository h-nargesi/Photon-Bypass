using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class AccountRepository(LocalDbContext context) : EditableRepository<AccountEntity>(context), IAccountRepository
{
    public async Task<AccountEntity?> GetAccount(string username)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Username)} = @username")
            .WithParameters(new { username }));

        return result.FirstOrDefault();
    }

    public async Task<AccountEntity?> GetAccountByMobile(string mobile)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Mobile)} = @mobile")
            .WithParameters(new { mobile }));

        return result.FirstOrDefault();
    }

    public async Task<AccountEntity?> GetAccountByEmail(string email)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Email)} = @email")
            .WithParameters(new { email }));

        return result.FirstOrDefault();
    }

    public async Task<IList<AccountEntity>> GetTargetArea(int accountId)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Parent)} = @accountId")
            .WithParameters(new { accountId }));

        return [.. result];
    }

    public async Task<IDictionary<int, AccountEntity>> GetAccounts(IEnumerable<int> userids)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Parent)} in (@userids)")
            .WithParameters(new { userids }));

        return result.ToDictionary(k => k.PermanentUserId);
    }
}
