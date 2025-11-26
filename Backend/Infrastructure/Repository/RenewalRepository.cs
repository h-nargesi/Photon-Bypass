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

        var result = await FindAsync(statement => statement
            .Where($"{nameof(RenewalEntity.AccountId)} = @account_id")
            .WithParameters(new { account_id }));

        return result.Select(x => x.RestrictedRealmId).FirstOrDefault();
    }
}