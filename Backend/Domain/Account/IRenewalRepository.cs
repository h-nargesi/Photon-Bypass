using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Account;

public interface IRenewalRepository : IEditableRepository<RenewalEntity>
{
    Task<RenewalEntity> LatestOf(string target);
}
