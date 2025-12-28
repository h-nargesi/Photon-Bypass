using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class AccountRepository(LocalDbContext context) : EditableRepository<AccountEntity>(context), IAccountRepository
{
    public async Task<AccountEntity?> GetAccount(int id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Id)} = @id")
            .WithParameters(new { id }));

        return result.FirstOrDefault();
    }

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

    public async Task<List<AccountEntity>> GetTargetArea(int account_id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Owner)} = @account_id")
            .WithParameters(new { account_id }));

        return [.. result];
    }

    public async Task<Dictionary<int, AccountEntity>> GetAccounts(IEnumerable<int> account_ids)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(AccountEntity.Id)} in @account_ids")
            .WithParameters(new { account_ids }));

        return result.ToDictionary(k => k.Id);
    }

    public async Task<Dictionary<string, int>> GetAccountIdByUsername(IEnumerable<string> usernames)
    {
        var sql = $"""
                   select {nameof(AccountEntity.Id)}, {nameof(AccountEntity.Username)}
                   from {TableName}
                   where {nameof(AccountEntity.Username)} in @usernames
                   """;

        var list = await QueryAsync(sql, new { usernames });

        return list.ToDictionary(pair => (string)pair.Username, pair => (int)pair.Id);
    }

    public async Task<Dictionary<int, string>> GetUsernamesByAccountId(IEnumerable<int> ids)
    {
        var sql = $"""
                   select {nameof(AccountEntity.Id)}, {nameof(AccountEntity.Username)}
                   from {TableName}
                   where {nameof(AccountEntity.Id)} in @ids
                   """;

        var list = await QueryAsync(sql, new { ids });

        return list.ToDictionary(pair => (int)pair.Id, pair => (string)pair.Username);
    }

    public async Task<int?> GetActiveAccountId(string username)
    {
        var result = await ExecuteScalarAsync<int>(
            $"select {nameof(AccountEntity.Id)} from {TableName} where {nameof(AccountEntity.Username)} = @username"
            , new { username });

        return result;
    }

    public async Task<int> CheckUniqueData(string username, string? email, string? mobile)
    {
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