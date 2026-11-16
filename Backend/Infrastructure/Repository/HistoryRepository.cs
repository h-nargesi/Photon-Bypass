using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class HistoryRepository(LocalDbContext context, Lazy<IPanelUserRepository> panel_user_repo) : EditableRepository<HistoryEntity>(context), IHistoryRepository
{
    public async Task<List<HistoryEntity>> GetHistory(int user_id, DateTime? from, DateTime? to)
    {
        var result = await FindAsync(statement =>
        {
            statement.Where($"{nameof(HistoryEntity.Target)} = @user_id")
                .WithParameters(new { user_id });

            if (from.HasValue)
            {
                statement.Where($"{nameof(HistoryEntity.Created)} >= @from")
                    .WithParameters(new { from });
            }

            if (to.HasValue)
            {
                statement.Where($"{nameof(HistoryEntity.Created)} <= @to")
                    .WithParameters(new { to });
            }
        });

        return [.. result];
    }

    public async Task<Dictionary<int, HistoryEntity>> GetLastByTitle(string title)
    {
        var sql = $"""
                   select h.*
                   from {TableName} h
                   join (select {nameof(HistoryEntity.Target)} as PanelUserId, max({nameof(HistoryEntity.Id)}) as LastId
                         from {TableName}
                         where {nameof(HistoryEntity.Title)} = @title
                         group by {nameof(HistoryEntity.Target)}) last
                        on h.{nameof(HistoryEntity.Id)} = last.LastId
                   """;

        var result = await QueryAsync<HistoryEntity>(sql, new { title });

        return result.ToDictionary(h => h.Target);
    }

    public async Task Save(string issuer_name, HistoryEntity entity)
    {
        entity.Issuer = await panel_user_repo.Value.GetActiveUserId(issuer_name);
        await Save(entity);
    }
}
