using Dapper;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Business;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;
using PhotonBypass.Tools;

namespace PhotonBypass.Infra.Repository;

class PlanStateRepository(LocalDbContext context) : DapperRepository<PlanStateEntity>(context), IPlanStateRepository
{
    private static readonly string TableName = EntityExtensions.GetTablename<PlanStateEntity>();
    private static readonly string Id = EntityExtensions.GetColumnName<PlanStateEntity>(x => x.Id);
    private static readonly string Username = EntityExtensions.GetColumnName<PlanStateEntity>(x => x.Username);
    private static readonly string TimeLeft = EntityExtensions.GetColumnName<PlanStateEntity>(x => x.TimeLeft);

    private static readonly string AccountDisabled =
        EntityExtensions.GetColumnName<PlanStateEntity>(x => x.AccountDisabled);

    private static readonly string TrafficLeft = EntityExtensions.GetColumnName<PlanStateEntity>(x => x.TrafficLeft);

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
        var query = $"""
select *
from {TableName} u
where {AccountDisabled} = 0 and exists (
 select *
 from (
     select *
     from {RenewalRepository.TableName} t
     where t.{RenewalRepository.AccountId} = u.{Id}
     order by t.{RenewalRepository.Id} desc
     limit 1
 ) t
 where t.{RenewalRepository.MonthLimit} is not null and (@percent * t.{RenewalRepository.MonthLimit} * 30) >= u.{TimeLeft}
    or t.{RenewalRepository.TrafficLimit} is not null and (@percent * to_gigabyte(t.{RenewalRepository.TrafficLimit})) >= u.{TrafficLeft}
)
""";

        var result = await Connection.QueryAsync<PlanStateEntity>(query,
            new { percent = PlanStateBusiness.AccountFinishingStatePercent });

        return [.. result];
    }

    public Task<int?> GetActiveAccountRealmId(string username)
    {
        throw new NotImplementedException();
    }
}