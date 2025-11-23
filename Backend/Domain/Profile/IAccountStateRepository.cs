using PhotonBypass.Domain.Profile.Model;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Profile;

public interface IAccountStateRepository
{
    Task<AccountStateEntity?> GetPlanState(int id);
    
    Task<AccountStateEntity?> GetPlanState(string username);
 
    Task<IList<AccountStateEntity>> GetPlanOverState(float percent);
}
