using PhotonBypass.Domain.Profile.Model;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Profile;

public interface IAccountProfileRepository : IEditableRepository<AccountProfileEntity>
{
    // Task<UserPlanStateEntity?> GetPlanState(int id);
    //
    // Task<UserPlanStateEntity?> GetPlanState(string username);
    //
    // Task<string?> GetRestrictedServerIP(int id);
    //
    // Task<IList<UserPlanStateEntity>> GetPlanOverState(float percent);
}
