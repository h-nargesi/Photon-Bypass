using PhotonBypass.Domain.Plan.Entity;

namespace PhotonBypass.Domain.Plan;

public interface IPlanStateRepository
{
    Task<PlanStateEntity?> GetPlanState(int id);
    
    Task<PlanStateEntity?> GetPlanState(string username);

    Task<IList<PlanStateEntity>> GetFinishingPlanState();
    
    Task<int?> GetActiveAccountRealmId(string username);
}
