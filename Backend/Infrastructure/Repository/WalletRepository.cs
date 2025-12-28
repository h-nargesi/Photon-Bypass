using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class WalletRepository(LocalDbContext context) : EditableRepository<WalletEntity>(context), IWalletRepository
{
    public async Task<List<WalletEntity>> GetTransactions(int account_id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(WalletEntity.AccountId)} = @account_id")
            .WithParameters(new { account_id }));

        return [.. result];
    }

    public async Task<List<WalletEntity>> GetNotPaid(int account_id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(WalletEntity.AccountId)} = @account_id and {nameof(WalletEntity.Status)} = @status")
            .WithParameters(new { account_id, status = BalanceStatus.Pending }));

        return [.. result];
    }

    public async Task<List<WalletEntity>> GetInvoice(string code)
    {
        IEnumerable<WalletEntity> result;

        if (code.StartsWith('w'))
        {
            result = await FindAsync(statement => statement
                .Where($"{nameof(WalletEntity.Id)} = @id")
                .WithParameters(new { id = code[1..] }));
        }
        else if (code.StartsWith('i'))
        {
            result = await FindAsync(statement => statement
                .Where($"{nameof(WalletEntity.InvoiceCode)} = @code")
                .WithParameters(new { code = code[1..] }));
        }
        else throw new Exception($"Invalid invoice-code={code}");

        return [.. result];
    }

    public async Task<Dictionary<int, int>> GetWalletsAmount(IEnumerable<int> ids)
    {
        var sql = $"""
                   select {nameof(WalletEntity.Id)}, {nameof(WalletEntity.Amount)} * {nameof(WalletEntity.Direction)} as Amount
                   from {TableName}
                   where {nameof(WalletEntity.Id)} = @ids
                   """;

        var result = await QueryAsync(sql, new { ids });

        return result.ToDictionary(k => (int)k.Id, v => (int)v.Amount);
    }

    public async Task<int> GenerateNewInvoiceCode()
    {
        var sql = $"""
                   select max({nameof(WalletEntity.InvoiceCode)})
                   from {TableName}
                   where {nameof(WalletEntity.InvoiceCode)} is not null
                   """;

        var max = (await ExecuteScalarAsync<int?>(sql)) ?? 10000;
        return max + 1;
    }

    public Task<int> GetBalance(int account_id)
    {
        var sql = $"""
                   select sum({nameof(WalletEntity.Amount)} * {nameof(WalletEntity.Direction)}) as Balance
                   from {TableName}
                   where {nameof(WalletEntity.AccountId)} = @account_id
                     and {nameof(WalletEntity.Status)} = {(sbyte)BalanceStatus.Completed}
                   """;

        return ExecuteScalarAsync<int>(sql, new { account_id });
    }
}