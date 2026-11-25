using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Account;

public interface IResetPassRepository
{
    Task<ResetPassEntity?> GetAccount(string hash_code);

    Task AddHashCode(ResetPassEntity hash_code);

    /// <summary>
    /// Invalidates every still-active reset code of a user before a new
    /// one is issued, so only the newest code can change the password.
    /// </summary>
    Task ExpireActiveCodes(int user_id);
}
