using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Account;

public interface IResetPassRepository : IEditableRepository<ResetPassEntity>
{
    Task<ResetPassEntity?> GetAccount(string hash_code);

    Task AddHashCode(ResetPassEntity hash_code);
}
