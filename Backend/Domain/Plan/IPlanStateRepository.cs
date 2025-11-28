using PhotonBypass.Domain.Plan.Entity;

namespace PhotonBypass.Domain.Plan;

public interface IPlanStateRepository
{
    Task<IList<PlanStateEntity>> GetAll();

    Task<PlanStateEntity?> GetPlanState(int account_id);
    
    Task<int?> GetActiveAccountRealmId(int account_id);
}
