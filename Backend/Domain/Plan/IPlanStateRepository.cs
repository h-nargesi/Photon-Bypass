using PhotonBypass.Domain.Plan.Entity;

namespace PhotonBypass.Domain.Plan;

public interface IPlanStateRepository
{
    Task<List<PlanStateEntity>> GetAll();

    /// <summary>Latest plan band of one VPN account (docs/22 §2: the view is keyed by VpnAccount.Id).</summary>
    Task<PlanStateEntity?> GetPlanState(int vpn_account_id);
}
