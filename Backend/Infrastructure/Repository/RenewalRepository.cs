using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class RenewalRepository(LocalDbContext context) : EditableRepository<RenewalEntity>(context), IRenewalRepository
{
    public async Task<int?> GetTopRestrictedRealmId(int account_id)
    {
        await OpenAsync();

        var sql = $"""
                   select {nameof(RenewalEntity.RestrictedRealmId)}
                   from {TableName}
                   where {nameof(RenewalEntity.AccountId)} = @account_id
                   """;

        var result = await QueryAsync<int>(sql, new { account_id });

        return result.FirstOrDefault();
    }
}