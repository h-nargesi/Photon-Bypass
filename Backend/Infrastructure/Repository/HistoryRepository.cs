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

    public async Task Save(string issuer_name, HistoryEntity entity)
    {
        entity.Issuer = await account_repo.Value.GetActiveAccountId(issuer_name);
        await Save(entity);
    }
}
