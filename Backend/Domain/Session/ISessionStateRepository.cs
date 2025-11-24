using PhotonBypass.Domain.Profile.Model;

namespace PhotonBypass.Domain.Profile;

public interface ISessionStateRepository
{
    Task<ASessionStateEntity?> GetASessionState(int id);
    
    Task<ASessionStateEntity?> GetASessionState(string username);
 
    Task<IList<ASessionStateEntity>> GetAccountFinishingState();
}
