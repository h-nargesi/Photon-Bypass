using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class AccountRepository(LocalDbContext context) : EditableRepository<AccountEntity>(context), IAccountRepository
{
    public async Task<AccountEntity?> GetAccount(int id)
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Id)} = @id")
            .WithParameters(new { id }));

        return result.FirstOrDefault();
    }

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

    public async Task<IList<AccountEntity>> GetTargetArea(int account_id)
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Parent)} = @account_id")
            .WithParameters(new { account_id }));

        return [.. result];
    }

    public async Task<IDictionary<int, AccountEntity>> GetAccounts(IEnumerable<int> account_ids)
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Parent)} in (@account_ids)")
            .WithParameters(new { account_ids }));

        return result.ToDictionary(k => k.Id);
    }

    public async Task<int?> GetActiveAccountId(string username)
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Username)} = @username")
            .WithParameters(new { username }));

        return result.Select(x => (int?)x.Id).FirstOrDefault();
    }

    public Task<bool> CheckUsername(string username)
    {
        throw new NotImplementedException();
    }
}
