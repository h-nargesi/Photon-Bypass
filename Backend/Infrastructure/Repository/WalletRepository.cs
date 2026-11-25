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

    public async Task<List<WalletEntity>> GetTransactions(int user_id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(WalletEntity.UserId)} = @user_id")
            .WithParameters(new { user_id }));

        return [.. result];
    }

    public async Task<List<WalletEntity>> GetInvoice(string target, int code)
    {
        var sql = $"""
                   select w.*
                   from {TableName} w
                   join {PanelUserRepository.TableName} a on w.{nameof(WalletEntity.UserId)} = a.{nameof(PanelUserEntity.Id)}
                   where w.{nameof(WalletEntity.InvoiceCode)} = @code
                     and a.{nameof(PanelUserEntity.Username)} = @target
                   """;

        var result = await QueryAsync<WalletEntity>(sql, new { target, code });

        return [..result];
    }

    public async Task<(List<WalletEntity> Rows, int TotalCount)> GetHistory(int user_id, int page, int page_size)
    {
        var total = await ExecuteScalarAsync<int>(
            $"""
             select count(*) as Total
             from {TableName}
             where {nameof(WalletEntity.UserId)} = @user_id
             """,
            new { user_id });

        var sql = $"""
                   select *
                   from {TableName}
                   where {nameof(WalletEntity.UserId)} = @user_id
                   order by {nameof(WalletEntity.Id)} desc
                   offset @skip rows fetch next @take rows only
                   """;

        var rows = await QueryAsync<WalletEntity>(sql, new { user_id, skip = (page - 1) * page_size, take = page_size });

        return ([.. rows], total);
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

    public async Task<List<int>> GetUserIdsBelowThreshold(int threshold)
    {
        var sql = $"""
                   select {nameof(WalletEntity.UserId)}
                   from {TableName}
                   where {nameof(WalletEntity.Status)} in ({CountedInBalanceStatusSql})
                   group by {nameof(WalletEntity.UserId)}
                   having sum({nameof(WalletEntity.Amount)} * {nameof(WalletEntity.Direction)}) < @threshold
                   """;

        var result = await QueryAsync<int>(sql, new { threshold });

        return [.. result];
    }

    public Task<int> GetBalance(int user_id)
    {
        var sql = $"""
                   select sum({nameof(WalletEntity.Amount)} * {nameof(WalletEntity.Direction)}) as Balance
                   from {TableName}
                   where {nameof(WalletEntity.UserId)} = @user_id
                     and {nameof(WalletEntity.Status)} in ({CountedInBalanceStatusSql})
                   """;

        return ExecuteScalarAsync<int>(sql, new { user_id });
    }
}
