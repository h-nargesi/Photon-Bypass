using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Vpn;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;
using PhotonBypass.Tools;

namespace PhotonBypass.Infra.Repository;

class TrafficDataRepository(LocalDbContext context) : EditableRepository<TrafficDataEntity>(context), ITrafficDataRepository
{
    readonly static string AccountTableName = EntityExtensions.GetTablename<AccountEntity>();

    public async Task<IList<TrafficDataEntity>> Fetch(string username, DateTime from)
    {
        var result = await FindAsync(statement => statement
            .Where($"{nameof(TrafficDataEntity.AccountId)} = (select {nameof(AccountEntity.Id)} from {AccountTableName} where {nameof(AccountEntity.Username)} = @username)")
            .WithParameters(new { username }));

        return [.. result];
    }
}
