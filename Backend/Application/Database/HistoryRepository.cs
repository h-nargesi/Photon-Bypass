using PhotonBypass.Domain;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Database.Local;

namespace PhotonBypass.Application.Database;

class HistoryRepository(ILocalRepository<HistoryEntity> repo) : EditableRepository<HistoryEntity>(repo)
{
    public async Task<IList<HistoryEntity>> GetHistory(string target, DateTime? from, DateTime? to)
    {
        var result = await repository.FindAsync(statement =>
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

        return result.ToList();
    }
}
