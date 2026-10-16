using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Infra.Database;
using PhotonBypass.Infra.Repository.DbContext;

namespace PhotonBypass.Infra.Repository;

class RenewalRepository(LocalDbContext context) : EditableRepository<RenewalEntity>(context), IRenewalRepository
{
    public async Task<RenewalEntity?> GetByWalletCredit(int wallet_id)
    {
        var renewals = await FindAsync(statement => statement
            .Where($"{nameof(RenewalEntity.WalletCredit)} = @wallet_id")
            .WithParameters(new { wallet_id }));

        return renewals.FirstOrDefault();
    }

    public async Task<Dictionary<int, RenewalEntity>> GetLatestByAccountIds(IEnumerable<int> account_ids)
    {
        var id_list = account_ids.ToList();

        if (id_list.Count == 0) return [];

        var renewals = await FindAsync(statement => statement
            .Where($"{nameof(RenewalEntity.AccountId)} in @account_ids")
            .WithParameters(new { account_ids = id_list }));

        return renewals.GroupBy(renewal => renewal.AccountId)
            .ToDictionary(group => group.Key, group => group.MaxBy(renewal => renewal.Id)!);
    }
}
