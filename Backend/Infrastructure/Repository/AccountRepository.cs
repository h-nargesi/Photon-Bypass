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

    public async Task<IDictionary<string, int>> GetAccountIdByUsername(IEnumerable<string> usernames)
    {
        await OpenAsync();

        var sql = $"""
                   select {nameof(AccountEntity.Id)}, {nameof(AccountEntity.Username)}
                   from {TableName}
                   where {nameof(AccountEntity.Username)} in (@usernames)
                   """;

        var list = await QueryAsync(sql, usernames);

        return list.ToDictionary(pair => (string)pair.Username, pair => (int)pair.Id);
    }

    public async Task<int?> GetActiveAccountId(string username)
    {
        await OpenAsync();

        var result = await ExecuteScalarAsync<int>(
            $"select {nameof(AccountEntity.Id)} from {TableName} where {nameof(AccountEntity.Username)} = @username"
            , new { username });

        return result;
    }

    public async Task<int> CheckUniqueData(string username, string? email, string? mobile)
    {
        await OpenAsync();

        var sql = $"""
                  select {nameof(AccountEntity.Username)}, {nameof(AccountEntity.Email)}, {nameof(AccountEntity.Mobile)}
                  from {TableName}
                  where {nameof(AccountEntity.Username)} = @username
                       {(email != null ? $"or {nameof(AccountEntity.Email)} = @email" : "")}
                       {(email != null ? $"or {nameof(AccountEntity.Mobile)} = @mobile" : "")}
                  """;

        var data = (await QueryAsync(sql, new { username, email, mobile })).ToList();

        if (data.Count < 1) return 0;

        return (data.Any(d => d.Username == username) ? 1 : 0) |
            (data.Any(d => d.Email == email) ? 2 : 0) |
            (data.Any(d => d.Mobile == mobile) ? 4 : 0);
    }
}