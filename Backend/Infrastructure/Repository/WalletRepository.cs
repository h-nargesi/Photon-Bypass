using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class WalletRepository(LocalDbContext context) : EditableRepository<WalletEntity>(context), IWalletRepository
{
    private static readonly string CountedInBalanceStatusSql =
        $"{(sbyte)BalanceStatus.Completed}, {(sbyte)BalanceStatus.Verifying}";

    public async Task<List<WalletEntity>> GetTransactions(int account_id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(WalletEntity.AccountId)} = @account_id")
            .WithParameters(new { account_id }));

        return [.. result];
    }

    public async Task<List<WalletEntity>> GetInvoice(string target, int code)
    {
        var sql = $"""
                   select w.*
                   from {TableName} w
                   join {AccountRepository.TableName} a on w.{nameof(WalletEntity.AccountId)} = a.{nameof(AccountEntity.Id)}
                   where w.{nameof(WalletEntity.InvoiceCode)} = @code
                     and a.{nameof(AccountEntity.Username)} = @target
                   """;

        var result = await QueryAsync<WalletEntity>(sql, new { target, code });

        return [..result];
    }

    public async Task<Dictionary<int, int>> GetWalletsAmount(IEnumerable<int> ids)
    {
        var id_list = ids.ToArray();

        if (id_list.Length < 1)
        {
            return [];
        }

        var sql = $"""
                   select {nameof(WalletEntity.Id)}, {nameof(WalletEntity.Amount)} * {nameof(WalletEntity.Direction)} as Amount
                   from {TableName}
                   where {nameof(WalletEntity.Id)} in @ids
                   """;

        var result = await QueryAsync(sql, new { ids = id_list });

        return result.ToDictionary(k => (int)k.Id, v => (int)v.Amount);
    }

    public async Task<List<int>> GetAccountIdsBelowThreshold(int threshold)
    {
        var sql = $"""
                   select {nameof(WalletEntity.AccountId)}
                   from {TableName}
                   where {nameof(WalletEntity.Status)} in ({CountedInBalanceStatusSql})
                   group by {nameof(WalletEntity.AccountId)}
                   having sum({nameof(WalletEntity.Amount)} * {nameof(WalletEntity.Direction)}) < @threshold
                   """;

        var result = await QueryAsync<int>(sql, new { threshold });

        return [.. result];
    }

    public Task<int> GetBalance(int account_id)
    {
        var sql = $"""
                   select sum({nameof(WalletEntity.Amount)} * {nameof(WalletEntity.Direction)}) as Balance
                   from {TableName}
                   where {nameof(WalletEntity.AccountId)} = @account_id
                     and {nameof(WalletEntity.Status)} in ({CountedInBalanceStatusSql})
                   """;

        return ExecuteScalarAsync<int>(sql, new { account_id });
    }
}
