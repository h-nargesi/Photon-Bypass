using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Plan;

public interface IResetPassRepository : IEditableRepository<ResetPassEntity>
{
    Task<ResetPassEntity?> GetAccount(string hash_code);

    Task AddHashCode(ResetPassEntity hash_code);
}
