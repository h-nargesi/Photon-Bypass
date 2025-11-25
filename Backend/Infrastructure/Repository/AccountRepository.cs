using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class AccountRepository(LocalDbContext context) : EditableRepository<AccountEntity>(context), IAccountRepository
{
    public async Task<AccountEntity?> GetAccount(string username)
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Username)} = @username")
            .WithParameters(new { username }));

        return result.FirstOrDefault();
    }

    public async Task<AccountEntity?> GetAccountByMobile(string mobile)
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Mobile)} = @mobile")
            .WithParameters(new { mobile }));

        return result.FirstOrDefault();
    }

    public async Task<AccountEntity?> GetAccountByEmail(string email)
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Email)} = @email")
            .WithParameters(new { email }));

        return result.FirstOrDefault();
    }

    public async Task<IList<AccountEntity>> GetTargetArea(int accountId)
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Parent)} = @accountId")
            .WithParameters(new { accountId }));

        return [.. result];
    }

    public async Task<IDictionary<int, AccountEntity>> GetAccounts(IEnumerable<int> userids)
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Parent)} in (@userids)")
            .WithParameters(new { userids }));

        return result.Where(a => a.ReferenceId.HasValue)
            .ToDictionary(k => k.ReferenceId.Value);
    }
}
