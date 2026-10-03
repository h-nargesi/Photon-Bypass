using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class RenewalRepository(LocalDbContext context) : EditableRepository<RenewalEntity>(context), IRenewalRepository
{
    public async Task<List<RenewalEntity>> GetNotPaid(int account_id)
    {
        var renewals = await FindAsync(statement => statement
            .Where($"{nameof(RenewalEntity.AccountId)} = @account_id and {nameof(RenewalEntity.Created)} is null")
            .WithParameters(new { account_id }));

        return [.. renewals];
    }

    public async Task<RenewalEntity?> GetByWalletCredit(int wallet_id)
    {
        var renewals = await FindAsync(statement => statement
            .Where($"{nameof(RenewalEntity.WalletCredit)} = @wallet_id")
            .WithParameters(new { wallet_id }));

        return renewals.FirstOrDefault();
    }
}