using PhotonBypass.Domain.Plan.Entity;

namespace PhotonBypass.Domain.Plan;

public interface IPlanStateRepository
{
    Task<PlanStateEntity?> GetPlanState(int id);
    
    Task<IList<PlanStateEntity>> GetFinishingPlanState();

    Task<int?> GetActiveAccountRealmId(string username);
}
