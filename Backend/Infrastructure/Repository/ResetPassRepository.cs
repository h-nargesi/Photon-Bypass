using Dapper.FastCrud;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class ResetPassRepository(LocalDbContext context) : DapperRepository<ResetPassEntity>(context), IResetPassRepository
{
    public async Task<ResetPassEntity?> GetAccount(string hash_code)
    {
        var sql = $"""
                   delete from {TableName}
                   output deleted.*
                   where {nameof(ResetPassEntity.HashCode)} = @hash_code
                   """;

        var result = await QueryAsync<ResetPassEntity>(sql, new { hash_code });

        return result.FirstOrDefault();
    }

    public async Task AddHashCode(ResetPassEntity hash_code)
    {
        await context.Connection.InsertAsync(hash_code);
    }

    public async Task ExpireActiveCodes(int account_id)
    {
        var now = DateTime.Now;

        var sql = $"""
                   update {TableName}
                   set {nameof(ResetPassEntity.ExpireDate)} = @now
                   where {nameof(ResetPassEntity.AccountId)} = @account_id
                     and {nameof(ResetPassEntity.ExpireDate)} > @now
                   """;

        await ExecuteAsync(sql, new { account_id, now });
    }
}
