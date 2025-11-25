using PhotonBypass.Domain.Session.Entity;

namespace PhotonBypass.Domain.Session;

public interface ISessionStateRepository
{
    Task<SessionStateEntity?> GetSessionState(int id);
    
    Task<SessionStateEntity?> GetSessionState(string username);
 
    Task<IList<SessionStateEntity>> GetAccountFinishingState();
}
