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

        return result.ToList();
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