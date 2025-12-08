using PhotonBypass.Domain.Plan.Entity;

namespace PhotonBypass.Domain.Plan;

public interface IPlanStateRepository
{
    Task<List<PlanStateEntity>> GetAll();

    Task<PlanStateEntity?> GetPlanState(int account_id);
    
    Task<(int AccountId, int? RealmId)?> GetActiveAccountRealmId(int account_id);
}
