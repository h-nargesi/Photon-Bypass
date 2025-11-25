using PhotonBypass.Domain.Account;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class HistoryRepository(LocalDbContext context) : EditableRepository<HistoryEntity>(context), IHistoryRepository
{
    public async Task<IList<HistoryEntity>> GetHistory(string target, DateTime? from, DateTime? to)
    {
        var result = await FindAsync(statement =>
        {
            statement.Where($"{nameof(HistoryEntity.Target)} = @target")
                .WithParameters(new { target });

            if (from.HasValue)
            {
                statement.Where($"{nameof(HistoryEntity.EventTime)} >= @from")
                    .WithParameters(new { from });
            }

            if (to.HasValue)
            {
                statement.Where($"{nameof(HistoryEntity.EventTime)} <= @to")
                    .WithParameters(new { to });
            }
        });

        return [.. result];
    }
}
