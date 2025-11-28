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

    public async Task<Dictionary<int, List<int>>> GetActiveAccountRealmId(IEnumerable<int> account_ids)
    {
        await OpenAsync();

        var sql = $"""
select {nameof(PlanStateEntity.Id)}, {nameof(PlanStateEntity.RestrictedRealmId)}
from {TableName} where {nameof(PlanStateEntity.Id)} in (@account_id)
""";
        
        var result = await QueryAsync(sql, new { account_ids });

        return result.Select(x => (RealmId: (int?)x.RestrictedRealmId, Id: (int)x.Id))
            .GroupBy(tuple => tuple.RealmId ?? 0)
            .ToDictionary(grouping => grouping.Key, grouping => grouping.Select(tuple => tuple.Id).ToList());
    }
}