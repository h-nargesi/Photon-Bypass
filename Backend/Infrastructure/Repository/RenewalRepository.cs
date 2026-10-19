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

        // a window function keeps the read O(1) rows per account: loading the
        // whole renewal history and reducing it in memory ran on every
        // collector cycle (docs/14 M31)
        var sql = $"""
                   select *
                   from (
                       select renewal.*,
                              ROW_NUMBER() over (
                                  partition by renewal.{nameof(RenewalEntity.AccountId)}
                                  order by renewal.{nameof(RenewalEntity.Id)} desc) as RowNumber
                       from {TableName} renewal
                       where renewal.{nameof(RenewalEntity.AccountId)} in @account_ids
                   ) ranked
                   where ranked.RowNumber = 1
                   """;

        var result = await QueryAsync<RenewalEntity>(sql, new { account_ids = id_list });

        return result.ToDictionary(renewal => renewal.AccountId);
    }
}
