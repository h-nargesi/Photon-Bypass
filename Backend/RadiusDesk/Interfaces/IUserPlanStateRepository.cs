using PhotonBypass.RadiusDesk.Entity;

namespace PhotonBypass.RadiusDesk.Interfaces;

public interface IUserPlanStateRepository
{
    Task<UserPlanStateEntity?> GetPlanState(int id);

    Task<UserPlanStateEntity?> GetPlanState(string username);

    Task<string?> GetRestrictedServerIP(int id);

    Task<IList<UserPlanStateEntity>> GetPlanOverState(float percent);
}
