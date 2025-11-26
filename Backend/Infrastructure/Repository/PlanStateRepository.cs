using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Business;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class PlanStateRepository(LocalDbContext context) : DapperRepository<PlanStateEntity>(context), IPlanStateRepository
{
    public async Task<PlanStateEntity?> GetPlanState(int id)
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"{nameof(PlanStateEntity.Id)} = @id")
            .WithParameters(new { id }));

        return result.FirstOrDefault();
    }

    public async Task<IList<PlanStateEntity>> GetFinishingPlanState()
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"{nameof(PlanStateEntity.TimeLeftPercent)} < @percent or {nameof(PlanStateEntity.TrafficLeftPercent)} < @percent")
            .WithParameters(new { percent = PlanStateBusiness.AccountFinishingStatePercent }));

        return [.. result];
    }
}