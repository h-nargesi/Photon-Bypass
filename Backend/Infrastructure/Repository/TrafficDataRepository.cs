using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class TrafficDataRepository(LocalDbContext context) : EditableRepository<TrafficDataEntity>(context), ITrafficDataRepository
{
    public async Task<List<TrafficDataEntity>> Fetch(string username, DateTime from)
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"""
{nameof(TrafficDataEntity.StartSession)} >= @from and
{nameof(TrafficDataEntity.AccountId)} = (
    select {nameof(AccountEntity.Id)} from {AccountRepository.TableName} where {nameof(AccountEntity.Username)} = @username
""")
            .WithParameters(new { username, from }));

        return [.. result];
    }

    public async Task<Dictionary<int, List<TrafficDataEntity>>> Fetch(IEnumerable<int> nas_ids, DateTime from)
    {
        await OpenAsync();

        var result = await FindAsync(statement => statement
            .Where($"{nameof(TrafficDataEntity.NasId)} in (@nas_ids) and {nameof(TrafficDataEntity.StartSession)} >= @from")
            .WithParameters(new { nas_ids, from }));

        return result.GroupBy(k => k.NasId)
            .ToDictionary(k => k.Key, v => v.ToList());
    }
}
