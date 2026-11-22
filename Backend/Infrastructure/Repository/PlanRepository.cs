using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class PlanRepository(LocalDbContext context) : EditableRepository<PlanEntity>(context), IPlanRepository
{
    private const string JunctionTable = "PlanRealm";

    private const string RenewalTable = "Renewal";

    public async Task<PlanEntity?> Find(int id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(PlanEntity.Id)} = @id")
            .WithParameters(new { id }));

        return result.FirstOrDefault();
    }

    public async Task<List<PlanEntity>> GetActives()
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(PlanEntity.IsActive)} = 1"));

        return [.. result];
    }

    public async Task<List<PlanEntity>> GetActiveByGroup(int group_id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(PlanEntity.IsActive)} = 1 and {nameof(PlanEntity.GroupId)} = @group_id")
            .WithParameters(new { group_id }));

        return [.. result];
    }

    public async Task<List<PlanEntity>> GetWebsitePlans(int group_id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(PlanEntity.IsActive)} = 1 and {nameof(PlanEntity.ShowOnWebsite)} = 1 and {nameof(PlanEntity.GroupId)} = @group_id")
            .WithParameters(new { group_id }));

        return [.. result];
    }

    public async Task<List<PlanEntity>> GetAll()
    {
        var result = await FindAsync(statement => statement
            .OrderBy($"{nameof(PlanEntity.GroupId)}, {nameof(PlanEntity.Id)}"));

        return [.. result];
    }

    public async Task<List<int>> GetRealmIds(int plan_id)
    {
        var sql = $"""
                   select {nameof(PlanRealmRow.RealmId)}
                   from {JunctionTable}
                   where {nameof(PlanRealmRow.PlanId)} = @plan_id
                   """;

        var rows = await QueryAsync<int>(sql, new { plan_id });

        return [.. rows];
    }

    public async Task SetRealmIds(int plan_id, IEnumerable<int> realm_ids)
    {
        // full replace inside the caller's transaction — the junction has no
        // independent lifecycle
        var sql_delete = $"""
                          delete from {JunctionTable}
                          where {nameof(PlanRealmRow.PlanId)} = @plan_id
                          """;

        await ExecuteAsync(sql_delete, new { plan_id });

        var rows = realm_ids
            .Distinct()
            .Select(realm_id => new PlanRealmRow { PlanId = plan_id, RealmId = realm_id })
            .ToList();

        if (rows.Count == 0) return;

        var sql_insert = $"""
                          insert into {JunctionTable} ({nameof(PlanRealmRow.PlanId)}, {nameof(PlanRealmRow.RealmId)})
                          values (@{nameof(PlanRealmRow.PlanId)}, @{nameof(PlanRealmRow.RealmId)})
                          """;

        await ExecuteAsync(sql_insert, rows);
    }

    public async Task<PlanEntity?> FindByName(string name)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(PlanEntity.Name)} = @name")
            .WithParameters(new { name }));

        return result.FirstOrDefault();
    }

    public Task<bool> IsReferencedByRenewals(int plan_id)
    {
        var sql = $"""
                   select top 1 1
                   from {RenewalTable}
                   where {RenewalTable}.PlanId = @plan_id
                   """;

        return ExecuteScalarAsync<bool>(sql, new { plan_id });
    }
}
