using PhotonBypass.Domain.Plan.Entity;

namespace PhotonBypass.Domain.Plan;

public interface IPlanStateRepository
{
    Task<PlanStateEntity?> GetPlanState(int account_id);
    
    Task<IList<PlanStateEntity>> GetFinishingPlanState();

    Task<int?> GetActiveAccountRealmId(int account_id);
}
