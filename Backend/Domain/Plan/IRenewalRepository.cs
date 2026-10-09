using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Plan;

public interface IRenewalRepository : IEditableRepository<RenewalEntity>
{
    Task<RenewalEntity?> GetByWalletCredit(int wallet_id);
}
