using PhotonBypass.Application.Plan.Model;
using PhotonBypass.Result;

namespace PhotonBypass.Application.Plan;

/// <summary>
/// Admin plan/group management (Docs/24 §5): the CRUD goes through the
/// repositories so the calculator pool auto-refreshes on every save (D4) —
/// direct SQL edits bypass the entity events and need the explicit reload.
/// </summary>
public interface IPlanManagementApplication
{
    /// <summary>All plans (inactive included) with group titles and realm sets.</summary>
    Task<ApiResult<List<PlanRowModel>>> List();

    /// <summary>Create or update a plan. The calculator code is compile-checked before saving.</summary>
    Task<ApiResult> Save(SavePlanModel model);

    /// <summary>Refused while any renewal references the plan.</summary>
    Task<ApiResult> Delete(int plan_id);

    Task<ApiResult<List<GroupRowModel>>> GroupList();

    Task<ApiResult> SaveGroup(SaveGroupModel model);

    /// <summary>Refused while the group still has members (or is the default group).</summary>
    Task<ApiResult> DeleteGroup(int group_id);

    /// <summary>D4: forced calculator pool rebuild for out-of-band script edits.</summary>
    Task<ApiResult> ReloadCalculators();
}
