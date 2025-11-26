using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Plan;

public interface IRenewalRepository : IEditableRepository<RenewalEntity>
{
    Task<RenewalEntity?> LatestOf(int account_id);

    Task<int?> LatestRealmIdOf(string target);
}
