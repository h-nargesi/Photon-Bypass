using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Business;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class PlanStateRepository(LocalDbContext context) : DapperRepository<PlanStateEntity>(context), IPlanStateRepository
{
    public async Task<IList<PlanStateEntity>> GetAll()
    {
        await OpenAsync();

        return [.. await FindAsync()];
    }

    public async Task<PlanStateEntity?> GetPlanState(int account_id)
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"{nameof(PlanStateEntity.Id)} = @account_id")
            .WithParameters(new { account_id }));

        return result.FirstOrDefault();
    }

    public async Task<int?> GetActiveAccountRealmId(int account_id)
    {
        await OpenAsync();

        var sql = $"select {nameof(PlanStateEntity.RestrictedRealmId)} from {TableName} where {nameof(PlanStateEntity.Id)} = @account_id";
        return await ExecuteScalarAsync<int?>(sql, new { account_id });
    }
}