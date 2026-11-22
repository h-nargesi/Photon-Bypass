using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Plan.Model;

namespace PhotonBypass.Domain.Plan;

public interface IPlanStateRepository
{
    Task<List<PlanStateEntity>> GetAll();

    /// <summary>Latest plan band of one VPN account (Docs/22 §2: the view is keyed by VpnAccount.Id).</summary>
    Task<PlanStateEntity?> GetPlanState(int vpn_account_id);

    /// <summary>
    /// Docs/24 §5: flat VPN-account report over the plan-state universe —
    /// server-side paging, whitelisted sorting (unlimited sorts last) and a
    /// username/owner search. <paramref name="owner_id"/> scopes a manager to
    /// subordinates; null = everyone.
    /// </summary>
    Task<(List<VpnAccountReportRow> Rows, int TotalCount)> SearchAccountReport(
        int? owner_id, string? term, string? sort, bool descending, int page, int page_size);
}
