using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Repository;

namespace PhotonBypass.Domain.Plan;

public interface IPlanRepository : IEditableRepository<PlanEntity>
{
    Task<PlanEntity?> Find(int id);

    /// <summary>All active plans — the calculator pool source (Docs/24 D4).</summary>
    Task<List<PlanEntity>> GetActives();

    /// <summary>Purchasable catalog of one group (active only, Docs/24 §5).</summary>
    Task<List<PlanEntity>> GetActiveByGroup(int group_id);

    /// <summary>D1: the public home page set — active + ShowOnWebsite of the default group.</summary>
    Task<List<PlanEntity>> GetWebsitePlans(int group_id);

    /// <summary>Admin CRUD list (inactive included).</summary>
    Task<List<PlanEntity>> GetAll();

    Task<List<int>> GetRealmIds(int plan_id);

    /// <summary>Replaces the realm set of the plan (M2M junction, Docs/24 §3).</summary>
    Task SetRealmIds(int plan_id, IEnumerable<int> realm_ids);

    /// <summary>Plan lookup by the unique internal name (duplicate guard, case-normalized by SQL collation).</summary>
    Task<PlanEntity?> FindByName(string name);

    /// <summary>Whether any renewal references the plan (delete guard).</summary>
    Task<bool> IsReferencedByRenewals(int plan_id);
}

/// <summary>Inline row of the M2M junction read (Dapper).</summary>
public class PlanRealmRow
{
    public int PlanId { get; set; }

    public int RealmId { get; set; }
}
