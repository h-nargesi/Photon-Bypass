using PhotonBypass.Domain.Account.Entity;

namespace PhotonBypass.Domain.Account;

public interface ISessionStateRepository
{
    Task<SessionStateEntity?> GetSessionState(int id);
    
    Task<SessionStateEntity?> GetSessionState(string username);
 
    Task<IList<SessionStateEntity>> GetAccountFinishingState();
}
