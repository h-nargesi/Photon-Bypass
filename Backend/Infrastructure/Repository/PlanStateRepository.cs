using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class PlanStateRepository(LocalDbContext context) : DapperRepository<PlanStateEntity>(context), IPlanStateRepository
{
    public async Task<List<PlanStateEntity>> GetAll()
    {
        return [.. await FindAsync()];
    }

    public async Task<PlanStateEntity?> GetPlanState(int account_id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(PlanStateEntity.Id)} = @account_id")
            .WithParameters(new { account_id }));

        return result.FirstOrDefault();
    }

    public async Task<(int, int?)?> GetActiveAccountRealmId(int account_id)
    {
        var sql = $@"select {nameof(PlanStateEntity.RestrictedRealmId)}
from {TableName}
where {nameof(PlanStateEntity.Id)} = @account_id";

        var result = (await QueryAsync(sql, new { account_id })).ToList();

        if (result.Count == 0)
        {
            return null;
        }

        return (account_id, result.First().RestrictedRealmId);
    }
}