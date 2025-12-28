using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Plan;

public interface IRenewalRepository : IEditableRepository<RenewalEntity>
{
    Task<List<RenewalEntity>> GetNotPaid(int account_id);
}
