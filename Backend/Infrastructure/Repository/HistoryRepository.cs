using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class HistoryRepository(LocalDbContext context, Lazy<IAccountRepository> account_repo) : EditableRepository<HistoryEntity>(context), IHistoryRepository
{
    public async Task<List<HistoryEntity>> GetHistory(string target, DateTime? from, DateTime? to)
    {
        var result = await FindAsync(statement =>
        {
            statement.Where($"{nameof(HistoryEntity.Target)} = @target")
                .WithParameters(new { target });

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
                   join (select {nameof(HistoryEntity.Target)} as AccountId, max({nameof(HistoryEntity.Id)}) as LastId
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
        entity.Issuer = await account_repo.Value.GetActiveAccountId(issuer_name);
        await Save(entity);
    }
}
