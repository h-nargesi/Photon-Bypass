namespace PhotonBypass.Domain.Account;

public interface IResetPassRepository
{
    Task<ResetPassEntity?> GetAccount(string hash_code);

    Task AddHashCode(ResetPassEntity hash_code);
}
