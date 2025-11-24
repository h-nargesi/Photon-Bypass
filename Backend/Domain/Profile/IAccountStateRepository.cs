using PhotonBypass.Domain.Profile.Model;

namespace PhotonBypass.Domain.Profile;

public interface IAccountStateRepository
{
    Task<AccountStateEntity?> GetAccountState(int id);
    
    Task<AccountStateEntity?> GetAccountState(string username);
 
    Task<IList<AccountStateEntity>> GetAccountFinishingState();
}
