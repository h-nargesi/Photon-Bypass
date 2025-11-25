using PhotonBypass.Domain.Session.Entity;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Session;

public interface IRenewalRepository : IEditableRepository<RenewalEntity>
{
    Task<RenewalEntity?> LatestOf(int account_id);

    Task<RenewalEntity?> LatestOf(string target);
}
