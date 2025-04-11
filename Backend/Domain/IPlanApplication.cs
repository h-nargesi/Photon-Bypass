using PhotonBypass.Domain.Model.Plan;
using PhotonBypass.Infra.Controller;

namespace PhotonBypass.Domain;

public interface IPlanApplication
{
    Task<ApiResult<UserPlanInfo>> GetPlanState(PlanStateContext context);

    Task<ApiResult<PlanInfo>> GetPlanInfo(PlanInfoContext context);

    Task<ApiResult<long>> Estimate(RnewalContext context);

    Task<ApiResult<RnewalResult>> Rnewal(RnewalContext context);
}
