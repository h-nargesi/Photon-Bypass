using Dapper.FastCrud;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class TelegramLinkRepository(LocalDbContext context) : DapperRepository<TelegramLinkEntity>(context), ITelegramLinkRepository
{
    public async Task<TelegramLinkEntity?> Consume(string code)
    {
        // delete-output makes the consumption atomic: two concurrent /start
        // messages with the same code cannot both win
        var sql = $"""
                   delete from {TableName}
                   output deleted.*
                   where {nameof(TelegramLinkEntity.Code)} = @code
                   """;

        var result = await QueryAsync<TelegramLinkEntity>(sql, new { code });

        return result.FirstOrDefault();
    }

    public async Task AddCode(TelegramLinkEntity code)
    {
        await context.Connection.InsertAsync(code);
    }

    public async Task ExpireActiveCodes(int account_id)
    {
        var now = DateTime.Now;

        var sql = $"""
                   update {TableName}
                   set {nameof(TelegramLinkEntity.ExpireDate)} = @now
                   where {nameof(TelegramLinkEntity.AccountId)} = @account_id
                     and {nameof(TelegramLinkEntity.ExpireDate)} > @now
                   """;

        await ExecuteAsync(sql, new { account_id, now });
    }
}
