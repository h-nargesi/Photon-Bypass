using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Business;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class TrafficDataRepository(LocalDbContext context)
    : EditableRepository<TrafficDataEntity>(context), ITrafficDataRepository
{
    public async Task<List<TrafficDataEntity>> Fetch(DateTime from)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(TrafficDataEntity.StartSession)} >= @from")
            .WithParameters(new { from }));

        return [.. result];
    }

    public async Task<List<TrafficDataEntity>> Fetch(int account_id, DateTime from)
    {
        var result = await FindAsync(statement => statement
            .Where(
                $"{nameof(TrafficDataEntity.AccountId)} = account_id and {nameof(TrafficDataEntity.StartSession)} >= @from")
            .WithParameters(new { account_id, from }));

        return [.. result];
    }

    public async Task<Dictionary<int, List<TrafficDataEntity>>> Fetch(IEnumerable<int> nas_ids, DateTime from)
    {
        var result = await FindAsync(statement => statement
            .Where(
                $"{nameof(TrafficDataEntity.NasId)} in (@nas_ids) and {nameof(TrafficDataEntity.StartSession)} >= @from")
            .WithParameters(new { nas_ids, from }));

        return result.GroupBy(k => k.NasId)
            .ToDictionary(k => k.Key, v => v.ToList());
    }

    public async Task<List<TrafficDataEntity>> FetchOpen()
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(TrafficDataEntity.EndSession)} is null"));

        return [.. result];
    }

    public async Task<Dictionary<int, DateTime?>> LastUpdateTime()
    {
        var sql = @$"
select r.{nameof(RealmEntity.Id)}, isnull(d.MinOpenStart, d.LastStart) as LastUpdate
from {RealmRepository.TableName} r
join (
    select n.{nameof(ServerEntity.RealmId)}
        , max({nameof(TrafficDataEntity.StartSession)}) as LastStart
        , min(case when {nameof(TrafficDataEntity.EndSession)} is null then {nameof(TrafficDataEntity.StartSession)} else null end) as MinOpenStart
    from {ServerRepository.TableName} as n left join {TableName} as t on n.{nameof(ServerEntity.Id)} = t.{nameof(TrafficDataEntity.NasId)}
    where n.{(nameof(ServerEntity.IsActive))} = 1
    group by n.{nameof(ServerEntity.RealmId)}
) d
where r.{nameof(RealmEntity.LastTrafficSync)} is null or
    r.{nameof(RealmEntity.LastTrafficSync)} < dateadd(second, @limit, getdate())";

        var min_open_activities = await QueryAsync(sql, new { limit = -RenewalBusiness.UpdateTrafficDataTimeSecondLimit });

        return min_open_activities.Select(x => (RealmId: (int)x.RealmId, LastUpdate: (DateTime?)x.LastUpdate))
            .ToDictionary(k => k.RealmId, v => v.LastUpdate);
    }
}