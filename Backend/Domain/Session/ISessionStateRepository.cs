using PhotonBypass.Domain.Session.Entity;

namespace PhotonBypass.Domain.Session;

public interface ISessionStateRepository
{
    Task<SessionStateEntity?> GetSessionState(int id);
    
    Task<IList<SessionStateEntity>> GetAccountFinishingState();
}
