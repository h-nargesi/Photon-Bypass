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
}
